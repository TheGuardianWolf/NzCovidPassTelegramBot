using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.Templates;
using NzCovidPassTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class RevokeModule : IBotUpdateModule
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassLinkerService _covidPassLinkerService;

        public RevokeModule(ITelegramBotClient client, ICovidPassLinkerService covidPassLinkerService, ILogger<RevokeModule> logger)
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
                    case CommandType.Revoke:
                        await Revoke(message);
                        return true;
                };
            }

            return false;
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

        public async Task<bool> BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message?.Chat.Type != ChatType.Private)
            {
                return false;
            }

            switch (callbackQuery.Data)
            {
                case "/confirmrevoke":
                    await RevokeConfirmStep(callbackQuery);
                    return true;
                case "/cancelrevoke":
                    await RevokeCancelStep(callbackQuery);
                    return true;
            }

            return false;
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
    }
}
