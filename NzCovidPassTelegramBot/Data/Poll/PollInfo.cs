namespace NzCovidPassTelegramBot.Data.Poll
{
    public class PollInfo
    {
        public string InlineMessageId { get; set; }
        public PollParticipant Creator { get; set; }
        public IList<PollParticipant> Participants { get; set; } = new List<PollParticipant>();
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdateDate { get; set; } = DateTime.UtcNow;

        public PollInfo(string inlineMessageId, PollParticipant creator)
        {
            InlineMessageId = inlineMessageId;
            Creator = creator;
        }
    }

    public class PollParticipant
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
    }
}
