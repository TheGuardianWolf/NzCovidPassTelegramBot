using System.ComponentModel.DataAnnotations;

namespace NzCovidPassTelegramBot.Data.Email
{
    public enum ContactRequestType
    {
        General,
        Issues,
        FeatureRequest,
        RoleApplication
    }

    public class ContactForm
    {
        [Required]
        [EmailAddress]
        public string From { get; set; } = "";
        [Required]
        public ContactRequestType Subject { get; set; }
        [Required]
        public string Message { get; set; } = "";
        public string Captcha { get; set; } = "";
    }
}
