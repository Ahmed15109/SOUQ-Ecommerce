using System.Net;
using System.Net.Mail;
using EcommerceApp.Options;
using Microsoft.Extensions.Options;

namespace EcommerceApp.Services
{
    public interface IAccountEmailService
    {
        bool IsConfigured { get; }
        Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);
    }

    public class AccountEmailService : IAccountEmailService
    {
        private readonly EmailSettings _settings;

        public AccountEmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public bool IsConfigured => _settings.IsConfigured;

        public async Task SendAsync(
            string recipient,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Email delivery is not configured.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(recipient);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
