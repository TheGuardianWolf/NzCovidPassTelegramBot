namespace NzCovidPassTelegramBot.Data.Bot
{
    public class BotConfiguration
    {
        public IEnumerable<User> Users { get; set; } = new List<User>();
    }
}
