using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineQueryResults;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public interface IBotInlineQueryReceiver
    {
        public Task<IEnumerable<InlineQueryResult>> BotOnInlineQueryReceived(InlineQuery inlineQuery);
    }
}
