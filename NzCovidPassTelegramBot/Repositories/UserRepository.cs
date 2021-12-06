using NzCovidPassTelegramBot.Data.Bot;

namespace NzCovidPassTelegramBot.Repositories
{
    public interface IUserRepository
    {
        Task<User?> Get(long id);
        Task<IEnumerable<User>> GetAll();
    }

    public class UserRepository : IUserRepository
    {
        private IEnumerable<User> Users { get; }

        public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
        {
            var botConfig = configuration.GetSection("Bot").Get<BotConfiguration>();
            Users = botConfig.Users;
        }

        public async Task<IEnumerable<User>> GetAll()
        {
            return await Task.FromResult(Users);
        }

        public async Task<User?> Get(long id)
        {
            return await Task.FromResult(Users.FirstOrDefault(x => x.UserId == id));
        }
    }
}
