using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.Templates;
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
        private readonly IUserService _userService;

        public HelpModule(IConfiguration configuration, ITelegramBotClient client, ILogger<HelpModule> logger, IUserService userService)
        {
            _logger = logger;
            _client = client;
            _userService = userService;
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
            if (message.Chat.Type != ChatType.Private)
            {
                return false;
            }

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
            var userCommandInfo = CommandType.Info.AsEnumerable();
            if (message.From is not null) 
            {
                if (await _userService.IsNotaryUser(message.From.Id))
                {
                    userCommandInfo = userCommandInfo.Concat(CommandType.NotaryInfo);
                }
             }

            var usage = string.Format(BotText.HelpInfo, _tgConfig.Hostname) + string.Join('\n', userCommandInfo.Select(x => $"{x.Command} - {x.Description}"));

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }
    }
}
