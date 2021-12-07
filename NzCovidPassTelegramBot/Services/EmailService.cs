using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Data.Email;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NzCovidPassTelegramBot.Services
{
    public interface IEmailService
    {
        Task<bool> SendContactFormEmail(ContactForm contactForm);
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger _logger;
        private readonly ISendGridClient _sendGridClient;
        private readonly SendGridConfiguration _sendGridConfiguration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration, ISendGridClient sendGridClient)
        {
            _logger = logger;
            _sendGridClient = sendGridClient;
            _sendGridConfiguration = configuration.GetSection("SendGrid").Get<SendGridConfiguration>();
        }

        public async Task<bool> SendContactFormEmail(ContactForm contactForm)
        {
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_sendGridConfiguration.FromAddress, _sendGridConfiguration.FromName),
                Subject = $"Vaxxy Contact Form - {contactForm.Subject}"
            };

            var contentWithMeta = @$"From
{contactForm.From}

Subject
{contactForm.Subject}

Message
{contactForm.Message}";

            msg.AddContent(MimeType.Text, contentWithMeta);
            msg.AddTo(new EmailAddress(_sendGridConfiguration.ContactAddress, _sendGridConfiguration.ContactName));

            var response = await _sendGridClient.SendEmailAsync(msg);

            _logger.LogInformation("Sending email {@email}", contactForm);

            if (response is null || !response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Email sending may have failed...");
                return false;
            }

            return true;
        }
    }
}
