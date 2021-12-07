using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.Templates;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class NotariseModule : IBotUpdateModule
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassLinkerService _covidPassLinkerService;
        private readonly IUserService _userService;
        private readonly TelegramConfiguration _tgConfig;

        public NotariseModule(IConfiguration configuration, ITelegramBotClient client, ICovidPassLinkerService covidPassLinkerService, IUserService userService, ILogger<LinkModule> logger)
        {
            _logger = logger;
            _client = client;
            _userService = userService;
            _covidPassLinkerService = covidPassLinkerService;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return await BotOnMessageReceived(update.Message!);
                case UpdateType.CallbackQuery:
                    return await BotOnCallbackQueryReceived(update.CallbackQuery!);
            };

            return false;
        }

        private async Task<bool> BotOnMessageReceived(Message message)
        {
            if (message.Chat.Type != ChatType.Private)
            {
                return false;
            }

            if (message.From is null || !await _userService.IsNotaryUser(message.From.Id))
            {
                return false;
            }

            if (message.Type == MessageType.Text)
            {
                switch (message.Text!.Split(' ')[0])
                {
                    case CommandType.Notarise:
                        await Notarise(message);
                        return true;
                };

                if (message.ForwardFrom is not null && message.ForwardFrom.Id != message.From.Id)
                {
                    await NotariseForwardedMessageStep(message);
                    return true;
                }
            }

            return false;
        }

        public async Task Notarise(Message message)
        { 
            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: BotText.NotariseInfo.Replace(".", "\\."),
                                                parseMode: ParseMode.MarkdownV2);
        }

        public async Task NotariseForwardedMessageStep(Message message)
        {
            var selfUserId = message.From!.Id;
            var targetUserId = message.ForwardFrom!.Id;
            if (!await _covidPassLinkerService.IsUserLinked(targetUserId))
            {
                const string notLinkedTemplate = @"This user does not have a valid covid pass linked to their account, please ask them to link their account first.";
                await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                    text: notLinkedTemplate);
                return; 
            }

            var pass = (await _covidPassLinkerService.GetPass(targetUserId))!;
            var notarisedByMe = pass.Verifiers.Contains(selfUserId);

            if (!notarisedByMe)
            {
                await NotariseConfirmStep(message);
            }
            else
            {
                await NotariseRevokeStep(message);
            }
        }

        public async Task NotariseConfirmStep(Message message)
        {
            var targetUserId = message.ForwardFrom!.Id;
            var targetUsername = message.ForwardFrom!.Username;

            var pass = (await _covidPassLinkerService.GetPass(targetUserId))!;

            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Confirm", $"/confirmnotarise"),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", "/cancelnotarise"),
                }
             });

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: string.Format(BotText.NotariseConfirm,
                                                    targetUserId,
                                                    targetUsername,
                                                    TimeZoneInfo.ConvertTimeFromUtc(pass.ValidFromDate, Program.NZTime).ToString("d"),
                                                    TimeZoneInfo.ConvertTimeFromUtc(pass.ValidToDate, Program.NZTime).ToString("d")),
                                                    replyMarkup: confirmKeyboard);
        }

        public async Task NotariseRevokeStep(Message message)
        {
            var selfUserId = message.From!.Id;
            var targetUserId = message.ForwardFrom!.Id;
            var targetUsername = message.ForwardFrom!.Username;

            var pass = (await _covidPassLinkerService.GetPass(targetUserId))!;

            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Revoke", $"/revokenotarise"),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", "/cancelnotarise"),
                }
             });

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: string.Format(BotText.RevokeNotariseConfirm,
                                                    targetUserId,
                                                    targetUsername,
                                                    TimeZoneInfo.ConvertTimeFromUtc(pass.ValidFromDate, Program.NZTime).ToString("d"),
                                                    TimeZoneInfo.ConvertTimeFromUtc(pass.ValidToDate, Program.NZTime).ToString("d")),
                                                    replyMarkup: confirmKeyboard);
        }

        public async Task<bool> BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message?.Chat.Type != ChatType.Private)
            {
                return false;
            }

            switch (callbackQuery.Data)
            {
                case "/confirmnotarise":
                    await NotariseStorageStep(callbackQuery);
                    return true;
                case "/revokenotarise":
                    await NotariseRevokeStep(callbackQuery);
                    return true;
                case "/cancelnotarise":
                    await NotariseCancelStep(callbackQuery);
                    return true;
            }

            return false;
        }

        public async Task NotariseStorageStep(CallbackQuery callbackQuery)
        {
            static async Task FailedNotarise(ITelegramBotClient client, Message message, string additionalReason)
            {
                const string failureText = @"Notarisation failed!.

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

            if (callbackQuery.Message is null || callbackQuery.Message.Text is null || callbackQuery.From is null)
            {
                // Can't do anything with no message and no sender
                return;
            }

            if (callbackQuery.Message.Chat.Type != ChatType.Private)
            {
                // Bad permissions, we only acknowledge the original sender
                return;
            }

            await _client.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.Typing);

            var userIdRegexp = new Regex(@"^User ID: (.+)", RegexOptions.Multiline);
            var match = userIdRegexp.Match(callbackQuery.Message.Text);

            if (!match.Success || match.Groups.Count < 2)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Could not find user id, please try again.");
                return;
            }

            var parseResult = long.TryParse(match.Groups[1].Value, out var userId);
            if (!parseResult)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Format of user id is invalid, please try again");
                return;
            }

            _logger.LogInformation("User {userId} is notarising {notarisedUserId}", userId, callbackQuery.From.Id);
            var notariseResult = await _covidPassLinkerService.NotarisePass(userId, callbackQuery.From.Id);

            if (!notariseResult)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Could not complete notarisation, storage service reports an error. Please try again later.");
                return;
            }

            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Notarisation has been successful!");
            await _client.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                              text: "Notarisation has been successful!");
        }

        public async Task NotariseRevokeStep(CallbackQuery callbackQuery)
        {
            static async Task FailedNotarise(ITelegramBotClient client, Message message, string additionalReason)
            {
                const string failureText = @"Notarisation removal failed!.

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

            if (callbackQuery.Message is null || callbackQuery.Message.Text is null || callbackQuery.From is null)
            {
                // Can't do anything with no message and no sender
                return;
            }

            if (callbackQuery.Message.Chat.Type != ChatType.Private)
            {
                // Bad permissions, we only acknowledge the original sender
                return;
            }

            await _client.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.Typing);

            var userIdRegexp = new Regex(@"^User ID: (.+)", RegexOptions.Multiline);
            var match = userIdRegexp.Match(callbackQuery.Message.Text);

            if (!match.Success || match.Groups.Count < 2)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Could not find user id, please try again.");
                return;
            }

            var parseResult = long.TryParse(match.Groups[1].Value, out var userId);
            if (!parseResult)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Format of user id is invalid, please try again");
                return;
            }

            _logger.LogInformation("User {userId} is revoking notarisation for {notarisedUserId}", userId, callbackQuery.From.Id);
            var notariseResult = await _covidPassLinkerService.RevokeNotarisePass(userId, callbackQuery.From.Id);

            if (!notariseResult)
            {
                await FailedNotarise(_client, callbackQuery.Message, "Could not complete notarisation removal, storage service reports an error. Please try again later.");
                return;
            }

            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Notarisation removal has been successful!");
            await _client.SendTextMessageAsync(chatId: callbackQuery.Message.Chat.Id,
                                              text: "Notarisation removal has been successful!");
        }

        public async Task NotariseCancelStep(CallbackQuery callbackQuery)
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
                                              text: "Notarisation action has been cancelled.");

            await _client.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Notarisation action has been cancelled.");
        }
    }
}
