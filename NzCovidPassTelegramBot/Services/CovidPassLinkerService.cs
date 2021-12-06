using NzCovidPass.Core;
using NzCovidPassTelegramBot.Data.CovidPass;
using NzCovidPassTelegramBot.Repositories;

namespace NzCovidPassTelegramBot.Services
{
    public interface ICovidPassLinkerService
    {
        Task<PassVerifierContext?> VerifyFullPass(string passPayload);
        Task<LinkedPass?> GetPass(long userId);
        Task<bool> LinkPass(LinkedPass pass);
        Task<bool> RevokePass(long userId);
        Task<bool> NotarisePass(long userId, long notaryId);
        Task<bool> RevokeNotarisePass(long userId, long notaryId);
        Task<bool> IsPassLinked(PassIdentifier passIdentifier);
        Task<bool> IsUserLinked(long userId);
    }

    public class CovidPassLinkerService : ICovidPassLinkerService
    {
        private readonly ILogger _logger;
        private readonly PassVerifier _passVerifier;
        private readonly ICovidPassRepository _covidPassRepository;
        private readonly IUserRepository _userRepository;

        public CovidPassLinkerService(ILogger<CovidPassLinkerService> logger, PassVerifier passVerifier, ICovidPassRepository covidPassRepository, IUserRepository userRepository)
        {
            _logger = logger;
            _passVerifier = passVerifier;
            _covidPassRepository = covidPassRepository;
            _userRepository = userRepository;
        }

        public async Task<PassVerifierContext?> VerifyFullPass(string passPayload)
        {
            var result = await _passVerifier.VerifyAsync(passPayload);
            return result;
        }

        public async Task<LinkedPass?> GetPass(long userId)
        {
            return await _covidPassRepository.Get(userId);
        }

        public async Task<bool> LinkPass(LinkedPass pass)
        {
            if (!pass.BetweenValidDates())
            {
                return false;
            }

            if (await IsPassLinked(pass.PassIdentifier))
            {
                return false;
            }

            await _covidPassRepository.Update(pass);

            return true;
        }

        public async Task<bool> RevokePass(long userId)
        {
            await _covidPassRepository.Remove(userId);

            return true;
        }

        public async Task<bool> NotarisePass(long userId, long notaryId)
        {
            var notary = await _userRepository.Get(notaryId);
            if (notary == null || !notary.HasClaim(Data.Bot.UserClaim.Notary))
            {
                return false;
            }

            var pass = await _covidPassRepository.Get(userId);
            if (pass is null || !pass.BetweenValidDates())
            {
                return false;
            }

            if (pass.Verifiers.Contains(notaryId))
            {
                return false;
            }

            pass.Verifiers = pass.Verifiers.Concat(new long[] { notary.UserId });

            await _covidPassRepository.Update(pass);

            return true;
        }
        
        public async Task<bool> RevokeNotarisePass(long userId, long notaryId)
        {
            var notary = await _userRepository.Get(notaryId);
            if (notary == null || !notary.HasClaim(Data.Bot.UserClaim.Notary))
            {
                return false;
            }

            var pass = await _covidPassRepository.Get(userId);
            if (pass is null || !pass.BetweenValidDates())
            {
                return false;
            }

            if (!pass.Verifiers.Contains(notaryId))
            {
                return false;
            }

            pass.Verifiers = pass.Verifiers.Where(x => x != notaryId).ToList();

            await _covidPassRepository.Update(pass);

            return true;
        }

        public async Task<bool> IsPassLinked(PassIdentifier passIdentifier)
        {
            var pass = await _covidPassRepository.Get(passIdentifier);

            return pass is not null && pass.BetweenValidDates();
        }

        public async Task<bool> IsUserLinked(long userId)
        {
            var pass = await _covidPassRepository.Get(userId);

            return pass is not null && pass.BetweenValidDates();
        }
    }
}
