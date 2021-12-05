using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace NzCovidPassTelegramBot.Data.CovidPass
{
    public class PassIdentifier
    {
        public string Hash { get; set; } = "";

        [JsonConstructor]
        private PassIdentifier()
        {
        }

        public PassIdentifier(string id)
        {
            Hash = HashId(id);
        }

        public PassIdentifier(Guid id)
        {
            Hash = HashId(id.ToString());
        }

        private string HashId(string id)
        {
            return Convert.ToHexString(SHA384.HashData(Encoding.UTF8.GetBytes(id)));
        }

        public bool Verify(string id)
        {
            return Hash == HashId(id);
        }

        public override string ToString()
        {
            return Hash;
        }
    }
}
