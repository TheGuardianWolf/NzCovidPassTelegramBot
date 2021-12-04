using NzCovidPassTelegramBot.Data.CovidPass;
using NzCovidPassTelegramBot.Data.Poll;
using NzCovidPassTelegramBot.Data.Shared;
using NzCovidPassTelegramBot.Data.Templates;
using System.Drawing;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using ZXing;

namespace NzCovidPassTelegramBot.Services
{
    public interface ITelegramBotService
    {
        Task ProcessUpdate(Update update);
    }

    public class TelegramBotService : ITelegramBotService
    {
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassLinkerService _covidPassLinkerService;
        private readonly ICovidPassPollService _covidPassPollService;
        private readonly ILogger _logger;
        private readonly TelegramConfiguration _tgConfig;

        public TelegramBotService(IConfiguration configuration, ILogger<TelegramBotClient> logger, ITelegramBotClient client, ICovidPassLinkerService covidPassLinkerService, ICovidPassPollService covidPassPollService)
        {
            _logger = logger;
            _client = client;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
            _covidPassLinkerService = covidPassLinkerService;
            _covidPassPollService = covidPassPollService;
        }

        public async Task ProcessUpdate(Update update)
        {
            var handler = update.Type switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => BotOnMessageReceived(update.Message!),
                UpdateType.EditedMessage => BotOnEditedMessageReceived(update.EditedMessage!),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery!),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery!),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult!),
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(exception);
            }
        }

        private async Task BotOnMessageReceived(Message message)
        {
            _logger.LogInformation("Receive message type: {messageType}", message.Type);
            if (message.Chat.Type != ChatType.Private)
            {
                // We only handle private messages here as linking is done on a 1-1 basis
                return;
            }

            await _client.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            var handlers = new[]
            { 
                BotOnMessageTextReceived,
                BotOnPhotoReceived
            };

            foreach (var handler in handlers)
            {
                var result = await handler(message);

                if (result)
                {
                    return;
                }
            }

            // No handlers used
            await Help(message);

            return;
        }

        private Task BotOnEditedMessageReceived(Message message)
        {
            // Ignore edited messages
            _logger.LogInformation("Receive edited message type: {messageType}", message.Type);
            return Task.CompletedTask;
        }

        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            _logger.LogInformation("Receive callback query data: {callbackData}", callbackQuery.Data);

            switch (callbackQuery.Data)
            {
                case "/confirmlink":
                    await LinkStorageStep(callbackQuery);
                    break;
                case "/cancellink":
                    await LinkCancelStep(callbackQuery);
                    break;
                case "/checkin":
                    await CheckIn(callbackQuery);
                    break;
                case "/confirmrevoke":
                    await RevokeConfirmStep(callbackQuery);
                    break;
                case "/cancelrevoke":
                    await RevokeCancelStep(callbackQuery);
                    break;
            }
        }

        private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
             _logger.LogInformation("Received inline query from: {inlineQueryFromId}", inlineQuery.From.Id);

            var text = new InputTextMessageContent(
                        string.Format(BotText.CovidPassCheck, DateTime.UtcNow.ToString("d"), inlineQuery.From.Username, _tgConfig.BotUsername, "No valid responses").Replace(".", "\\.").Replace("-", "\\-")
                    );
            text.ParseMode = ParseMode.MarkdownV2;
            var response = new InlineQueryResultArticle(
                    id: "/startcheckin",
                    title: "Request Covid Pass status",
                    inputMessageContent: text
                );;

            response.ReplyMarkup = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Check in", $"/checkin")
                }
             });

            InlineQueryResult[] results = {
                response
            };

            await _client.AnswerInlineQueryAsync(inlineQueryId: inlineQuery.Id,
                                                    results: results,
                                                    cacheTime: 0);
        }

        private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
            _logger.LogInformation("Received inline result: {chosenInlineResultId}", chosenInlineResult.ResultId);

            switch (chosenInlineResult.ResultId)
            {
                case "/startcheckin":
                    await CreatePollForCheckIn(chosenInlineResult);
                    break;
            }

            return;
        }

        private async Task CreatePollForCheckIn(ChosenInlineResult chosenInlineResult)
        {
            var poll = await _covidPassPollService.NewPoll(chosenInlineResult.InlineMessageId!, chosenInlineResult.From.Id, chosenInlineResult.From?.Username ?? "");

            await SyncPassPollInfoWithMessage(poll);
        }

        private async Task<bool> BotOnMessageTextReceived(Message message)
        {
            if (message.Type != MessageType.Text)
            {
                return false;
            }

            // Delegate action
            switch (message.Text!.Split(' ')[0])
            {
                case CommandType.Start:
                    await Help(message);
                    break;
                case CommandType.Link:
                    await Link(message);
                    break;
                case CommandType.Check:
                    await Check(message);
                    break;
                case CommandType.Revoke:
                    await Revoke(message);
                    break;
                default:
                    return false;
            };

            return true;
        }

        /// <summary>
        /// Usually should handle special content requested by one of the main commands
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task<bool> BotOnPhotoReceived(Message message)
        {
            if (message.Type != MessageType.Photo)
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

        public async Task Help(Message message)
        {
            _logger.LogTrace("Help information requested");

            const string usageFormatString = @"This bot and service is made to link a NZ Covid Pass with your Telegram account. It can be used to share anonymous vaccine information for event organisation or otherwise. A single pass can only be attached to one telegram account.

No personal data is directly stored through this service, any stored data will be in a hashed format.

Please visit {0} for more details.

Usage:

";
            var usage = string.Format(usageFormatString, _tgConfig.Hostname) + string.Join('\n', CommandType.Info.Select(x => $"{x.Command} - {x.Description}"));

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }

        public async Task Link(Message message)
        {
            // Check if linked or not linked and provide link instructions
            var isUserLinked = await _covidPassLinkerService.IsUserLinked(message.From!.Id); // Link step only follows after sender is verified via message.From

            const string linkInstructionString = @"{0}

If you would like to link a NZ Covid pass, please upload an image containing the QR code from your Covid pass.

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
                const string failedToScanText = "Sent photo could not be parsed by QR code scanner. Please ensure the QR code is visible and the image is under 20 MiB.";

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

            using var ms = new MemoryStream();
            var photoResult = await _client.GetInfoAndDownloadFileAsync(photoId, ms);

            if (photoResult is null)
            {
                await FailedToScanAction(_client, message);
                return;
            }

            var barcodeReader = new BarcodeReader();

#pragma warning disable CA1416 // It works in docker linux with the right packages
            using var photoBitmap = (Bitmap)Image.FromStream(ms);

            var qrCode = barcodeReader.Decode(photoBitmap);
#pragma warning restore CA1416 // Validate platform compatibility
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

        public async Task CheckIn(CallbackQuery callbackQuery)
        {
            static async Task FailedCheckIn(ITelegramBotClient client, CallbackQuery callbackQuery)
            {
                await client.AnswerCallbackQueryAsync(
                           callbackQueryId: callbackQuery.Id,
                           text: "An error has occured :(");
            }

            if (callbackQuery.InlineMessageId is null)
            {
                await FailedCheckIn(_client, callbackQuery);
                return;
            }

            var isUserLinked = await _covidPassLinkerService.IsUserLinked(callbackQuery.From.Id);

            // Check if sender is verified
            if (isUserLinked)
            {
                // Create check existing poll
                var poll = _covidPassPollService.GetPoll(callbackQuery.InlineMessageId);

                if (poll is null)
                {
                    await FailedCheckIn(_client, callbackQuery);
                    return;
                }

                var updatedPoll = await _covidPassPollService.AddParticipantToPoll(callbackQuery.InlineMessageId, callbackQuery.From.Id, callbackQuery.From.Username ?? "");

                if (updatedPoll is null)
                {
                    await FailedCheckIn(_client, callbackQuery);
                    return;
                }

                await _client.AnswerCallbackQueryAsync(
                    callbackQueryId: callbackQuery.Id,
                    text: "You have checked in!");

                await SyncPassPollInfoWithMessage(updatedPoll);
            }
            else
            {
                await _client.AnswerCallbackQueryAsync(
                    callbackQueryId: callbackQuery.Id,
                    text: $"Your account is not linked to a Covid pass, please message @{_tgConfig.BotUsername} to link.");
            }
        }

        public async Task Check(Message message)
        {
            // Send convert to inline, then proceed with inline options
            const string checkTemplate = @"Click the button below to request a group or user's vaccine pass link status\(es\).

This bot can be called inline with `@{0}` in any private or group chats to initiate a poll.

This will prompt users to volunteer their Covid pass statuses.";

            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithSwitchInlineQuery("Check Covid pass status"),
                }
             });

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: string.Format(checkTemplate,
                                                    _tgConfig.BotUsername).Replace(".", "\\."),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: confirmKeyboard);
        }

        public async Task Revoke(Message message)
        {
            // Check if linked or not linked and provide link instructions
            var isUserLinked = await _covidPassLinkerService.IsUserLinked(message.From!.Id); // Link step only follows after sender is verified via message.From

            if (isUserLinked)
            {
                const string linkInstructionString = @"{0}

To remove your link, please click the button below.";

                var instruction = string.Format(linkInstructionString, BotText.LinkedPreamble);

                var confirmKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Confirm", $"/confirmrevoke"),
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Cancel", "/cancelrevoke"),
                    }
                 });

                await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: instruction.Replace(".", "\\."),
                    parseMode: ParseMode.MarkdownV2,
                    replyMarkup: confirmKeyboard);
            }
            else
            {
                const string linkInstructionString = @"{0}

There is nothing to revoke!";

                var instruction = string.Format(linkInstructionString, BotText.NotLinkedPreamble);

                await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                    text: instruction.Replace(".", "\\.").Replace("!", "\\!"),
                    parseMode: ParseMode.MarkdownV2);
            }
        }

        public async Task RevokeConfirmStep(CallbackQuery callbackQuery)
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

            await _covidPassLinkerService.RevokePass(callbackQuery.From.Id);

            await _client.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                              text: "Link has been revoked.");
            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Link has been revoked.");
        }

        public async Task RevokeCancelStep(CallbackQuery callbackQuery)
        {
            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Revoke link canceled.");

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
                                              text: "Revoke link has been cancelled.");
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {updateType}", update.Type);
            return Task.CompletedTask;
        }

        private Task HandleErrorAsync(Exception exception)
        {
            var ErrorMessage = exception switch
            {
                //ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);
            return Task.CompletedTask;
        }

        private async Task SyncPassPollInfoWithMessage(PollInfo pollInfo)
        {
            _logger.LogInformation("Updating poll {inlineMessageId}", pollInfo.InlineMessageId);

            var markup = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Check in", $"/checkin")
                }
             });

            var participantsListString = string.Join(", ", pollInfo.Participants.Select(x => $"@{x.Username}".Replace("_", "\\_")));

            if (!pollInfo.Participants.Any())
            {
                participantsListString = "No valid responses";
            }

            await _client.EditMessageTextAsync(
                inlineMessageId: pollInfo.InlineMessageId,
                text: string.Format(
                    BotText.CovidPassCheck, 
                    pollInfo.CreationDate.ToString("d"), 
                    pollInfo.Creator.Username, 
                    _tgConfig.BotUsername,
                    participantsListString
                ).Replace(".", "\\.").Replace("-", "\\-"),
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: markup);
        }
    }
}
