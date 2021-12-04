using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using NzCovidPassTelegramBot.Data.Poll;

namespace NzCovidPassTelegramBot.Repositories
{
    public interface IInlineMessagePollRepository
    {
        Task<PollInfo?> Get(string inlineMessageId);
        Task Update(PollInfo poll);
    }

    public class InlineMessagePollRepository : IInlineMessagePollRepository
    {
        private const string _cachePrefix = "inlineMessagePoll_";
        private readonly ILogger _logger;
        private readonly IDistributedCache _store;

        public InlineMessagePollRepository(ILogger<InlineMessagePollRepository> logger, IDistributedCache store)
        {
            _logger = logger;
            _store = store;
        }

        public async Task<PollInfo?> Get(string inlineMessageId)
        {
            _logger.LogTrace("Getting poll {inlineMessageId}", inlineMessageId);

            var data = await _store.GetStringAsync(_cachePrefix + inlineMessageId);

            if (data is null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<PollInfo>(data);
        }

        public async Task Update(PollInfo poll)
        {
            _logger.LogTrace("Updating poll {inlineMessageId}", poll.InlineMessageId);

            var data = JsonConvert.SerializeObject(poll);

            await _store.SetStringAsync(_cachePrefix + poll.InlineMessageId, data);
        }
    }
}
