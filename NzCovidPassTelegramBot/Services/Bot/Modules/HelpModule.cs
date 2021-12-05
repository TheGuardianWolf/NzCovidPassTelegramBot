using NzCovidPassTelegramBot.Data.Shared;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NzCovidPassTelegramBot.Services.Bot.Modules
{
    public class HelpModule : IBotUpdateModule
    {
        private readonly ILogger _logger;
        private readonly ITelegramBotClient _client;
        private readonly TelegramConfiguration _tgConfig;

        public HelpModule(IConfiguration configuration, ITelegramBotClient client, ILogger<HelpModule> logger)
        {
            _logger = logger;
            _client = client;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
        }

        public async Task<bool> Process(Update update)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return await BotOnMessageReceived(update.Message!);
            };

            return false;
        }

        private async Task<bool> BotOnMessageReceived(Message message)
        {
            if (message.Type == MessageType.Text)
            {
                switch (message.Text!.Split(' ')[0])
                {
                    case CommandType.Start:
                        await Help(message);
                        return true;
                };
            }

            return false;
        }

        public async Task Help(Message message)
        {
            const string usageFormatString = @"This bot and service is made to link a NZ Covid Pass with your Telegram account. It can be used to share anonymous vaccine information for event organisation or otherwise. A single pass can only be attached to one telegram account.

No personal data is directly stored through this service, any stored data will be in a hashed format.

Please visit {0} for more details.

Usage:

";
            var usage = string.Format(usageFormatString, _tgConfig.Hostname) + string.Join('\n', CommandType.Info.Select(x => $"{x.Command} - {x.Description}"));

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }
    }
}
