namespace NzCovidPassTelegramBot.Data.Bot
{
    public class SendGridConfiguration
    {
        public string ApiKey { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "";
        public string ContactAddress { get; set; } = "";
        public string ContactName { get; set; } = "";
    }
}
