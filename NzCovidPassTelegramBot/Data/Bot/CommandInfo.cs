namespace NzCovidPassTelegramBot.Data.Bot
{
    public class CommandInfo
    {
        public string Command { get; set; }
        public string Description { get; set; }

        public CommandInfo(string command, string description)
        {
            Command = command;
            Description = description;
        }
    }
}
