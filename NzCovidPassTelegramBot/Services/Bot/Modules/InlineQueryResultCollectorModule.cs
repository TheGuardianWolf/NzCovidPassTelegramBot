using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class InlineQueryResultCollectorModule : IBotUpdateModule
    {
        private readonly IEnumerable<IBotInlineQueryReceiver> _inlineQueryReceivers;
        private readonly ITelegramBotClient _client;

        public InlineQueryResultCollectorModule(ITelegramBotClient client, IEnumerable<IBotUpdateModule> updateModules)
        {
            _client = client;
            _inlineQueryReceivers = updateModules.Select(x => x as IBotInlineQueryReceiver).Where(x => x is not null).Select(x => x!).ToList();
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.InlineQuery:
                    return await BotOnInlineQueryReceived(update.InlineQuery!);
            };

            return false;
        }

        private async Task<bool> BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
            var queryResults = new List<InlineQueryResult>();
            foreach (var receiver in _inlineQueryReceivers)
            {
                queryResults.AddRange(await receiver.BotOnInlineQueryReceived(inlineQuery));
            }

            await _client.AnswerInlineQueryAsync(inlineQueryId: inlineQuery.Id,
                                                    results: queryResults,
                                                    isPersonal: true);

            return true;
        }
    }
}
