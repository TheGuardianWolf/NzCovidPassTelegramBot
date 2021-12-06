﻿using NzCovidPassTelegramBot.Data.Bot;
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
            const string usageFormatString = @"This bot and service is made to link a NZ Covid Pass with your Telegram account. It can be used to share anonymous vaccine information for event organisation or otherwise. A single pass can only be attached to one telegram account.

No personal data is directly stored through this service, any stored data will be in a hashed format.

Please visit {0} for more details.

Usage:

";
            var userCommandInfo = CommandType.Info.AsEnumerable();
            if (message.From is not null) 
            {
                if (await _userService.IsNotaryUser(message.From.Id))
                {
                    userCommandInfo = userCommandInfo.Concat(CommandType.NotaryInfo);
                }
             }

            var usage = string.Format(usageFormatString, _tgConfig.Hostname) + string.Join('\n', CommandType.Info.Select(x => $"{x.Command} - {x.Description}"));

            await _client.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }
    }
}
