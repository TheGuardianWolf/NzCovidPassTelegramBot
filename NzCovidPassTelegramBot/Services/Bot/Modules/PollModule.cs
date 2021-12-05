using NzCovidPassTelegramBot.Data.Poll;
using NzCovidPassTelegramBot.Data.Shared;
using NzCovidPassTelegramBot.Data.Templates;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class PollModule : IBotUpdateModule
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassPollService _covidPassPollService;
        private readonly TelegramConfiguration _tgConfig;

        public PollModule(IConfiguration configuration, ITelegramBotClient client, ICovidPassPollService covidPassPollService, ILogger<PollModule> logger)
        {
            _logger = logger;
            _client = client;
            _covidPassPollService = covidPassPollService;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return await BotOnMessageReceived(update.Message!);
                case UpdateType.InlineQuery:
                    return await BotOnInlineQueryReceived(update.InlineQuery!);
                case UpdateType.ChosenInlineResult:
                    return await BotOnChosenInlineResultReceived(update.ChosenInlineResult!);
            };

            return false;
        }

        private async Task<bool> BotOnMessageReceived(Message message)
        {
            if (message.Type == MessageType.Text)
            {
                switch (message.Text!.Split(' ')[0])
                {
                    case CommandType.Check:
                        await Check(message);
                        return true;
                };
            }

            return false;
        }

        private async Task<bool> BotOnInlineQueryReceived(InlineQuery inlineQuery)
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
                ); ;

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
            return true;
        }

        private async Task<bool> BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
            _logger.LogInformation("Received inline result: {chosenInlineResultId}", chosenInlineResult.ResultId);

            switch (chosenInlineResult.ResultId)
            {
                case "/startcheckin":
                    await CreatePollForCheckIn(chosenInlineResult);
                    return true;
            }

            return false;
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

        private async Task CreatePollForCheckIn(ChosenInlineResult chosenInlineResult)
        {
            var poll = await _covidPassPollService.NewPoll(chosenInlineResult.InlineMessageId!, chosenInlineResult.From.Id, chosenInlineResult.From?.Username ?? "");

            await SyncPassPollInfoWithMessage(poll);
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
