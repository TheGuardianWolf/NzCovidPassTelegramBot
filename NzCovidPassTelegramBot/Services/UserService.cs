using NzCovidPassTelegramBot.Repositories;

namespace NzCovidPassTelegramBot.Services
{
    public interface IUserService
    {
        Task<bool> IsAdminUser(long userId);
        Task<bool> IsNotaryUser(long userId);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> IsNotaryUser(long userId)
        {
            var user = await _userRepository.Get(userId);
            return await Task.FromResult(user?.HasClaim(Data.Bot.UserClaim.Notary) ?? false);
        }

        public async Task<bool> IsAdminUser(long userId)
        {
            var user = await _userRepository.Get(userId);
            return await Task.FromResult(user?.HasClaim(Data.Bot.UserClaim.Admin) ?? false);
        }
    }
}
