using Newtonsoft.Json;
using System.Text;

namespace NzCovidPassTelegramBot.Data.CovidPass
{
    

    public class LinkedPass
    {
        public long UserId { get; set; }
        public PassIdentifier PassIdentifier { get; set; }
        public DateTime ValidToDate { get; set; }
        public DateTime ValidFromDate { get; set; }
        public IEnumerable<long> Verifiers { get; set; } = new long[] { };

        public LinkedPass(long userId, PassIdentifier passIdentifier, DateTime validToDate, DateTime validFromDate)
        {
            UserId = userId;
            PassIdentifier = passIdentifier;
            ValidToDate = validToDate;
            ValidFromDate = validFromDate;
        }

        public bool BetweenValidDates()
        {
            return DateTime.UtcNow < ValidToDate && DateTime.UtcNow >= ValidFromDate;
        }

        public string GenerateCode()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)));
        }

        public static LinkedPass? FromCode(string code)
        {
            return JsonConvert.DeserializeObject<LinkedPass>(Encoding.UTF8.GetString(Convert.FromBase64String(code)));
        }
    }
}
