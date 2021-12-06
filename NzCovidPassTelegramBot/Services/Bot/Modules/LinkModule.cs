using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.CovidPass;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZXing;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class LinkModule : IBotUpdateModule
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassLinkerService _covidPassLinkerService;

        public LinkModule(ITelegramBotClient client, ICovidPassLinkerService covidPassLinkerService, ILogger<LinkModule> logger)
        {
            _logger = logger;
            _client = client;
            _covidPassLinkerService = covidPassLinkerService;
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return await BotOnMessageReceived(update.Message!);
                case UpdateType.CallbackQuery:
                    return await BotOnCallbackQueryReceived(update.CallbackQuery!);
            }

            return false;
        }

        private async Task<bool> BotOnMessageReceived(Message message)
        {
            if (message.Chat.Type != ChatType.Private)
            {
                return false;
            }

            if (message.Type == MessageType.Text)
            {
                switch (message.Text!.Split(' ')[0])
                {
                    case CommandType.Link:
                        await Link(message);
                        return true;
                };
            }
            else if (message.Type == MessageType.Photo)
            {
                return await LinkPhoto(message);
            }

            return false;
        }

        private async Task<bool> LinkPhoto(Message message)
        {
            if (message.Chat.Type != ChatType.Private)
            {
                return false;
            }

            // Handle images as Covid Pass QR
            // Requred: Not forwarded from different user
            if (message.From is not null && (message.ForwardFrom is null || message.ForwardFrom?.Id == message.From.Id))
            {
                await LinkImageStep(message);
                return true;
            }

            return false;
        }

        public async Task<bool> BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message?.Chat.Type != ChatType.Private)
            {
                return false;
            }

            switch (callbackQuery.Data)
            {
                case "/confirmlink":
                    await LinkStorageStep(callbackQuery);
                    return true;
                case "/cancellink":
                    await LinkCancelStep(callbackQuery);
                    return true;
            }

            return false;
        }

        public async Task Link(Message message)
        {
            // Check if linked or not linked and provide link instructions
            var isUserLinked = await _covidPassLinkerService.IsUserLinked(message.From!.Id); // Link step only follows after sender is verified via message.From

            const string linkInstructionString = @"{0}

If you would like to link or update a NZ Covid pass, please upload an image containing the QR code from your Covid pass.

The quickest way may be to take a screenshot from your mobile phone screen while the pass is open, ensuring that the QR code is clearly visible.";

            const string notLinkedPreamble = "Your Telegram account is *NOT LINKED* to a Covid pass.";
            const string linkedPreamble = "Your Telegram account is *LINKED* to a Covid pass";

            var linkInstruction = string.Format(linkInstructionString, isUserLinked ? linkedPreamble : notLinkedPreamble);

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                text: linkInstruction.Replace(".", "\\."),
                parseMode: ParseMode.MarkdownV2);
        }

        public async Task LinkImageStep(Message message)
        {
            // User provide QR image
            static async Task FailedToScanAction(ITelegramBotClient client, Message message)
            {
                const string failedToScanText = "Sent photo could not be parsed by QR code scanner. Please ensure the QR code is visible and the image is under 20 MiB (PNG/JPEG for best results).";

                await client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                    text: failedToScanText);
            }

            // Grab the photo with a width smaller than or equal to 2000 to keep processing times low
            var photoId = message.Photo!.LastOrDefault(x => x.Width <= 2000)?.FileId;

            if (photoId is null)
            {
                await FailedToScanAction(_client, message);
                return;
            }

            byte[] pixelArray;
            using (var ms = new MemoryStream())
            {
                var photoResult = await _client.GetInfoAndDownloadFileAsync(photoId, ms);
                pixelArray = ms.ToArray();
            }

            Result? qrCode;
            try
            {
                var barcodeReader = new ZXing.ImageSharp.BarcodeReader<Rgba32>();

                using var rawImage = Image.Load(pixelArray, out var rawImageFormat);
                using var photoBitmap = rawImage?.CloneAs<Rgba32>();

                if (photoBitmap is null)
                {
                    await FailedToScanAction(_client, message);
                    return;
                }

                qrCode = barcodeReader.Decode(photoBitmap);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not decode barcode for {userId}", message.From!.Id);
                await FailedToScanAction(_client, message);
                return;
            }

            if (qrCode is null || string.IsNullOrWhiteSpace(qrCode.Text))
            {
                await FailedToScanAction(_client, message);
                return;
            }

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  parseMode: ParseMode.MarkdownV2,
                                                  text: @$"Barcode/qrcode data:

```
{qrCode.Text}
```");

            await LinkCheckPassStep(message, qrCode.Text);
        }

        public async Task LinkCheckPassStep(Message message, string payload)
        {
            // Pass to verification service
            static async Task FailedCheckAction(ITelegramBotClient client, Message message, string additionalReason)
            {
                const string failureText = @"A readable barcode/qrcode was detected but could not be verified.

Additional reason(s):

{0}";

                await client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                    text: string.Format(failureText, additionalReason));
            }

            var result = await _covidPassLinkerService.VerifyFullPass(payload);

            if (result is null)
            {
                await FailedCheckAction(_client, message, "No information returned from MoH service.");
                return;
            }

            if (!result.HasSucceeded)
            {
                await FailedCheckAction(_client, message, string.Join("\n", result.FailureReasons.Select(x => $"[{x.Code}] - {x.Message}")));
                return;
            }

            if (result.Pass is null || result.Token.Cti == Guid.Empty)
            {
                await FailedCheckAction(_client, message, "The submitted pass was valid but invalid data was received from verification service.");
                return;
            }

            var isLinked = await _covidPassLinkerService.IsPassLinked(new PassIdentifier(result.Token.Cti));
            if (isLinked)
            {
                await FailedCheckAction(_client, message, "This pass has been linked and cannot be re-linked. Please revoke or recover this link before proceeding.");
                return;
            }

            var validFrom = result.Token.ValidFrom;
            var validTo = result.Token.ValidTo;
            var givenName = result.Pass.GivenName;
            var familyName = result.Pass.FamilyName;
            var dob = result.Pass.DateOfBirth;

            // From will always exist at this step
            var linkedPass = new LinkedPass(message.From!.Id, new PassIdentifier(result.Token.Cti), result.Token.ValidTo, result.Token.ValidFrom);

            if (DateTime.UtcNow >= validTo)
            {
                await FailedCheckAction(_client, message, "This pass has expired, please request a new one.");
                return;
            }

            const string verifyPassTemplate = @"This Covid pass can be linked to your account, please check its details are correct. These details will not be saved.

Given name: {0}
Family name: {1}
Date of birth: {2}
Valid from: {3}
Valid to: {4}

Unique link code: `{5}`
";

            // Confirm details via user
            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Confirm", $"/confirmlink"),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", "/cancellink"),
                }
             });

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: string.Format(verifyPassTemplate,
                                                    givenName,
                                                    familyName,
                                                    dob.ToString("d"),
                                                    TimeZoneInfo.ConvertTimeFromUtc(validFrom, Program.NZTime).ToString("d"),
                                                    TimeZoneInfo.ConvertTimeFromUtc(validTo, Program.NZTime).ToString("d"),
                                                    linkedPass.GenerateCode()
                                                ).Replace(".", "\\."),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: confirmKeyboard);
        }

        public async Task LinkStorageStep(CallbackQuery callbackQuery)
        {
            static async Task FailedLinkAction(ITelegramBotClient client, Message message, string additionalReason)
            {
                const string failureText = @"Covid pass link failed!.

Additional reason(s):

{0}";

                await client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                    text: string.Format(failureText, additionalReason));
            }

            // Remove associated inline keyboard
            if (callbackQuery.Message is not null)
            {
                await _client.EditMessageReplyMarkupAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId);
            }

            if (callbackQuery.Message is null || callbackQuery.Message.Text is null)
            {
                // Can't do anything with no message
                return;
            }

            if (callbackQuery.Message.Chat.Type != ChatType.Private)
            {
                // Bad permissions, we only acknowledge the original sender
                return;
            }

            await _client.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.Typing);

            var linkCodeRegexp = new Regex(@"^Unique link code: (.+)", RegexOptions.Multiline);
            var match = linkCodeRegexp.Match(callbackQuery.Message.Text);

            if (!match.Success || match.Groups.Count < 2)
            {
                await FailedLinkAction(_client, callbackQuery.Message, "Could not find unique link code, please try linking again.");
                return;
            }

            var linkCode = match.Groups[1].Value;
            var linkedPass = LinkedPass.FromCode(linkCode);
            if (linkedPass is null)
            {
                await FailedLinkAction(_client, callbackQuery.Message, "Format of unique link code is invalid, please try linking again.");
                return;
            }

            var linkResult = await _covidPassLinkerService.LinkPass(linkedPass);

            if (!linkResult)
            {
                await FailedLinkAction(_client, callbackQuery.Message, "Could not complete link, storage service reports an error. Please try again later.");
                return;
            }

            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Link has been successful!");
            await _client.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                              text: "Link has been successful!");
        }

        public async Task LinkCancelStep(CallbackQuery callbackQuery)
        {
            // Remove associated inline keyboard
            if (callbackQuery.Message is null)
            {
                // Can't do anything with no message
                return;
            }

            if (callbackQuery.Message.Chat.Type != ChatType.Private)
            {
                // Bad permissions, we only acknowledge the original sender
                return;
            }

            await _client.EditMessageReplyMarkupAsync(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId);

            await _client.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                              text: "Link has been cancelled.");

            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Link has been cancelled.");
        }

    }
}
