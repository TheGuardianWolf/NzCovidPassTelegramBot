using NzCovidPassTelegramBot.Data.Shared;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace NzCovidPassTelegramBot.Services.Hosted
{

    public class ConfigureTelegramWebhookHostedService : IHostedService
    {
        private readonly ILogger<ConfigureTelegramWebhookHostedService> _logger;
        private readonly IServiceProvider _services;
        private readonly ITelegramBotClient _telegramBotClient;
        private readonly TelegramConfiguration _config;

        public ConfigureTelegramWebhookHostedService(ILogger<ConfigureTelegramWebhookHostedService> logger,
            IServiceProvider serviceProvider,
                                IConfiguration configuration,
                                ITelegramBotClient telegramBotClient)
        {
            _logger = logger;
            _services = serviceProvider;
            _config = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
            _telegramBotClient = telegramBotClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            using var scope = _services.CreateScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            // Configure custom endpoint per Telegram API recommendations:
            // https://core.telegram.org/bots/api#setwebhook
            // If you'd like to make sure that the Webhook request comes from Telegram, we recommend
            // using a secret path in the URL, e.g. https://www.example.com/<token>.
            // Since nobody else knows your bot's token, you can be pretty sure it's us.
            var webhookAddress = @$"{_config.Hostname}/api/webhook/receive/{_config.ApiToken}";
            _logger.LogInformation("Setting webhook: {webhookAddress}", webhookAddress);
            await _telegramBotClient.SetWebhookAsync(
                url: webhookAddress,
                allowedUpdates: Array.Empty<UpdateType>(),
                cancellationToken: cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            // Remove webhook upon app shutdown
            _logger.LogInformation("Removing webhook");
            await _telegramBotClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
        }
    }
}