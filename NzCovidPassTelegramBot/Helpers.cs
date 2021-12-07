using System.Text.RegularExpressions;

namespace NzCovidPassTelegramBot
{
    public static class Helpers
    {
        public static string InsertSpaceBetweenCaps(string input)
        {
            return Regex.Replace(input, @"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))", " $0");
        }
    }
}
