using NzCovidPassTelegramBot.Services.Bot;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace NzCovidPassTelegramBot.Services
{
    public interface ITelegramBotService
    {
        Task ProcessUpdate(Update update);
    }

    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger _logger;
        private readonly Cortex _botCortex;

        public TelegramBotService(ILogger<TelegramBotClient> logger, Cortex botCortex)
        {
            _logger = logger;
            _botCortex = botCortex;
        }

        public Task ProcessUpdate(Update update) => _botCortex.Process(update);
    }
}
