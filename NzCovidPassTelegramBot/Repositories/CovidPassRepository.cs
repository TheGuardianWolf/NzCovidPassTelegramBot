using Microsoft.Extensions.Caching.Distributed;
using NzCovidPassTelegramBot.Data.CovidPass;
using Newtonsoft.Json;

namespace NzCovidPassTelegramBot.Repositories
{
    public interface ICovidPassRepository
    {
        Task<LinkedPass?> Get(long userId);
        Task<LinkedPass?> Get(PassIdentifier passIdentifier);
        Task Update(LinkedPass pass);
        Task Remove(long userId);
    }

    public class CovidPassRepository : ICovidPassRepository
    {
        private const string _cachePrefix = "covidPass_";
        private readonly ILogger _logger;
        private readonly IDistributedCache _store;

        public CovidPassRepository(ILogger<CovidPassRepository> logger, IDistributedCache store)
        {
            _logger = logger;
            _store = store;
        }

        public async Task<LinkedPass?> Get(long userId)
        {
            _logger.LogTrace("Getting covid pass for {userId}", userId);

            var data = await _store.GetStringAsync(_cachePrefix + userId.ToString());

            if (data is null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<LinkedPass>(data);
        }

        public async Task<LinkedPass?> Get(PassIdentifier passIdentifier)
        {
            _logger.LogTrace("Getting covid pass via identifier {passIdentifier}", passIdentifier.ToString());

            var userIdString = await _store.GetStringAsync(_cachePrefix + passIdentifier.ToString());

            var success = long.TryParse(userIdString, out var userId);

            if (!success)
            {
                return null;
            }

            return await Get(userId);
        }

        public async Task Update(LinkedPass pass)
        {
            _logger.LogTrace("Updating pass for {userId}", pass.UserId);

            var previousPass = await Get(pass.UserId);
            if (previousPass is not null)
            {
                // Remove previous pass first
                await Remove(previousPass.UserId);
            }

            var data = JsonConvert.SerializeObject(pass);

            var options = new DistributedCacheEntryOptions { AbsoluteExpiration = pass.ValidToDate };

            await Task.WhenAll(
                _store.SetStringAsync(_cachePrefix + pass.UserId.ToString(), data, options),
                // Store reference via passid
                _store.SetStringAsync(_cachePrefix + pass.PassIdentifier.ToString(), pass.UserId.ToString(), options)
            );
        }

        public async Task Remove(long userId)
        {
            _logger.LogTrace("Removing pass for {userId}", userId);

            var pass = await Get(userId);

            if (pass == null)
            {
                return;
            }

            await Task.WhenAll(
                _store.RemoveAsync(_cachePrefix + pass.UserId.ToString()),
                _store.RemoveAsync(_cachePrefix + pass.PassIdentifier.ToString())
            );
        }
    }
}
