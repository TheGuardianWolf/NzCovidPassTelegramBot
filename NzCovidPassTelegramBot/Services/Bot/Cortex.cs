using NzCovidPassTelegramBot.Services.Bot.Modules;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NzCovidPassTelegramBot.Services.Bot
{
    public class Cortex
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<IBotUpdateModule> _coreModules;

        public Cortex(ILogger<Cortex> logger, IEnumerable<IBotUpdateModule> coreModules)
        {
            _logger = logger;
            _coreModules = coreModules;
        }

        public async Task Process(Update update)
        {
            var handled = false;
            foreach (var module in _coreModules)
            {
                try
                {
                    handled = await module.Process(update);
                }
                catch (Exception exception)
                {
                    await HandleErrorAsync(exception);
                }

                if (handled)
                {
                    return;
                }
            }

            await UnhandledUpdateHandlerAsync(update);
        }

        private Task UnhandledUpdateHandlerAsync(Update update)
        {
            _logger?.LogDebug("Unhandled update type: {updateType}", update.Type);
            return Task.CompletedTask;
        }

        private Task HandleErrorAsync(Exception exception)
        {
            var ErrorMessage = exception switch
            {
                //ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger?.LogDebug("HandleError: {ErrorMessage}", ErrorMessage);
            return Task.CompletedTask;
        }
    }

    public static class CortexServiceExtensions
    {
        public static IServiceCollection AddBotCortex(this IServiceCollection services)
        {
            services.AddTransient<Cortex>();
            services.AddTransient<IBotUpdateModule, RevokeModule>();
            services.AddTransient<IBotUpdateModule, PollModule>();
            services.AddTransient<IBotUpdateModule, LinkModule>();
            services.AddTransient<IBotUpdateModule, HelpModule>();

            return services;
        }
    }
}
