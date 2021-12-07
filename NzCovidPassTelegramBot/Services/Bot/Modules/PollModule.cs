using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.Poll;
using NzCovidPassTelegramBot.Data.Templates;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class PollModule : IBotUpdateModule, IBotInlineQueryReceiver
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly ICovidPassPollService _covidPassPollService;
        private readonly ICovidPassLinkerService _covidPassLinkerService;
        private readonly TelegramConfiguration _tgConfig;

        public PollModule(IConfiguration configuration, ITelegramBotClient client, ICovidPassLinkerService covidPassLinkerService, ICovidPassPollService covidPassPollService, ILogger<PollModule> logger)
        {
            _logger = logger;
            _client = client;
            _covidPassPollService = covidPassPollService;
            _covidPassLinkerService = covidPassLinkerService;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return await BotOnMessageReceived(update.Message!);
                case UpdateType.ChosenInlineResult:
                    return await BotOnChosenInlineResultReceived(update.ChosenInlineResult!);
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

        public async Task<IEnumerable<InlineQueryResult>> BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
            var text = new InputTextMessageContent(
                        string.Format(BotText.CovidPassCheck,
                        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Program.NZTime).ToString("d"),
                        inlineQuery.From.Username, 
                        _tgConfig.BotUsername, 
                        "No valid responses").Replace(".", "\\.").Replace("-", "\\-")
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

            return await Task.FromResult(results);
        }

        private async Task<bool> BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
            switch (chosenInlineResult.ResultId)
            {
                case "/startcheckin":
                    await CreatePollForCheckIn(chosenInlineResult);
                    return true;
            }

            return false;
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

            var notarisedParticipantIds = await _covidPassLinkerService.FilterNotarisedUsers(pollInfo.Participants.Select(x => x.Id));

            var participantsListString = string.Join(", ", pollInfo.Participants.Select(x => $"@{x.Username}{(notarisedParticipantIds.Contains(x.Id) ? " ✔" : "")}".Replace("_", "\\_")));

            if (!pollInfo.Participants.Any())
            {
                participantsListString = "No valid responses";
            }

            await _client.EditMessageTextAsync(
                inlineMessageId: pollInfo.InlineMessageId,
                text: string.Format(
                    BotText.CovidPassCheck,
                    TimeZoneInfo.ConvertTimeFromUtc(pollInfo.CreationDate, Program.NZTime).ToString("d"),
                    pollInfo.Creator.Username,
                    _tgConfig.BotUsername,
                    participantsListString
                ).Replace(".", "\\.").Replace("-", "\\-"),
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: markup);
        }

        private async Task<bool> BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            switch (callbackQuery.Data)
            {
                case "/checkin":
                    await CheckIn(callbackQuery);
                    return true;
            }

            return false;
        }

        public async Task Check(Message message)
        {
            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithSwitchInlineQuery("Check Covid pass status"),
                }
             });

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                text: string.Format(BotText.CheckInfo,
                                                    _tgConfig.BotUsername).Replace(".", "\\."),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: confirmKeyboard);
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
                var poll = await _covidPassPollService.GetPoll(callbackQuery.InlineMessageId);

                if (poll is null)
                {
                    await FailedCheckIn(_client, callbackQuery);
                    return;
                }

                var userId = callbackQuery.From.Id;
                var username = callbackQuery.From.Username ?? "";
                if (!poll.Participants.Any(x => x.Id == userId && x.Username == username))
                {
                    var updatedPoll = await _covidPassPollService.AddParticipantToPoll(callbackQuery.InlineMessageId, callbackQuery.From.Id, callbackQuery.From.Username ?? "");

                    if (updatedPoll is null)
                    {
                        await FailedCheckIn(_client, callbackQuery);
                        return;
                    }

                    await SyncPassPollInfoWithMessage(updatedPoll);
                }

                await _client.AnswerCallbackQueryAsync(
                    callbackQueryId: callbackQuery.Id,
                    text: "You have checked in!");
            }
            else
            {
                await _client.AnswerCallbackQueryAsync(
                    callbackQueryId: callbackQuery.Id,
                    text: $"Your account is not linked to a Covid pass, please message @{_tgConfig.BotUsername} to link.");
            }
        }
    }
}
