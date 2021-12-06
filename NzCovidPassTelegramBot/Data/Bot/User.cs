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
        [JsonProperty(ItemConverterType=typeof(StringEnumConverter))]
        public IEnumerable<UserClaim> UserClaims { get; set; } = new List<UserClaim>();

        public bool HasClaim(UserClaim testClaim)
        {
            return UserClaims.Contains(testClaim);
        }
    }
}
