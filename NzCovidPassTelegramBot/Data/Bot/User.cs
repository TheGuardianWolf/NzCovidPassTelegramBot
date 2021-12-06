using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NzCovidPassTelegramBot.Data.Bot
{
    public enum UserClaim
    {
        Admin = 'A',
        Notary = 'N',
    }

    public class User
    {
        public long UserId { get; set; }
        public UserClaim[] UserClaims { get; set; } = new UserClaim[] { };

        public bool HasClaim(UserClaim testClaim)
        {
            return UserClaims.Contains(testClaim);
        }
    }
}
