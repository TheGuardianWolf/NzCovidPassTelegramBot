using Telegram.Bot.Types;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public interface IBotUpdateModule
    {
        public Task<bool> Process(Update update);
    }

    public interface IBotMetaUpdateModule : IBotUpdateModule
    {
    }
}
