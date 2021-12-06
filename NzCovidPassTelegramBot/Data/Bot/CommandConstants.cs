namespace NzCovidPassTelegramBot.Data.Bot
{
    public class CommandType
    {
        public const string Start = "/start";
        public const string Link = "/link";
        public const string Check = "/check";
        public const string Revoke = "/revoke";
        public const string Recover = "/recover";
        public const string Notarise = "/notarise";

        public static readonly CommandInfo[] Info =
        {
            new CommandInfo(Start, "this message"),
            new CommandInfo(Link, "link NZ Covid pass with your Telegram account via this bot"),
            new CommandInfo(Check, "check Covid pass status for a group or user"),
            new CommandInfo(Revoke, "remove my link"),
            //new CommandInfo(Recover, "recover my link from another Telegram account via email")
        };

        public static readonly CommandInfo[] NotaryInfo =
        {
            new CommandInfo(Notarise, "Notarise / vouch for a linked covid pass with your account")
        };
    }
}
