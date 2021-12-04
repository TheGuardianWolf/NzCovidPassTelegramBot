namespace NzCovidPassTelegramBot.Data.Templates
{
    public class BotText
    {
        public const string CovidPassCheck = @"*NZ Covid Pass Check - {0}*

@{1} has requested Covid pass statuses for members of this chat.

Message @{2} if you haven't already to link your Covid pass.

With a valid pass, clicking the button below will reveal that you have a valid Covid pass, no other information is given.

Members valid: {3}.";


        public const string NotLinkedPreamble = "Your Telegram account is *NOT LINKED* to a Covid pass.";
        public const string LinkedPreamble = "Your Telegram account is *LINKED* to a Covid pass";
    }
}
