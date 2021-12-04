using Microsoft.Extensions.Caching.Distributed;
using NzCovidPassTelegramBot.Data.Poll;
using NzCovidPassTelegramBot.Repositories;

namespace NzCovidPassTelegramBot.Services
{
    public interface ICovidPassPollService
    {
        Task<PollInfo?> AddParticipantToPoll(string inlineMessageId, long participantId, string participantUsername);
        Task<PollInfo?> GetPoll(string inlineMessageId);
        Task<PollInfo> NewPoll(string inlineMessageId, long creatorId, string creatorUsername);
    }

    public class CovidPassPollService : ICovidPassPollService
    {
        private readonly ILogger<CovidPassPollService> _logger;
        private readonly IInlineMessagePollRepository _inlineMessagePollRepository;

        public CovidPassPollService(ILogger<CovidPassPollService> logger, IInlineMessagePollRepository inlineMessagePollRepository)
        {
            _logger = logger;
            _inlineMessagePollRepository = inlineMessagePollRepository;
        }

        public async Task<PollInfo?> GetPoll(string inlineMessageId)
        {
            _logger.LogTrace("Getting poll {inlineMessageId}", inlineMessageId);
            return await _inlineMessagePollRepository.Get(inlineMessageId);
        }

        public async Task<PollInfo> NewPoll(string inlineMessageId, long creatorId, string creatorUsername)
        {
            _logger.LogTrace("Creating new poll {inlineMessageId} from creator: {creatorId} {creatorUserName}", inlineMessageId, creatorId, creatorUsername);
            var poll = new PollInfo(inlineMessageId, new PollParticipant
            {
                Id = creatorId,
                Username = creatorUsername
            });
            await _inlineMessagePollRepository.Update(poll);

            return poll;
        }

        public async Task<PollInfo?> AddParticipantToPoll(string inlineMessageId, long participantId, string participantUsername)
        {
            _logger.LogTrace("Adding participant to poll {inlineMessageId}: {participantId} {participantUsername}", inlineMessageId, participantId, participantUsername);
            var poll = await _inlineMessagePollRepository.Get(inlineMessageId);

            if (poll is null)
            {
                return null;
            }

            // Check participants first
            var existingParticipant = poll.Participants.FirstOrDefault(p => p.Id == participantId);
            if (existingParticipant is null)
            {
                // Otherwise add
                poll.Participants.Add(new PollParticipant
                {
                    Id = participantId,
                    Username = participantUsername
                });
            }
            else
            {
                // Sync usernames
                existingParticipant.Username = participantUsername;
            }

            poll.LastUpdateDate = DateTime.UtcNow;

            await _inlineMessagePollRepository.Update(poll);

            return poll;
        }
    }
}
