using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace KLTN_Registration_System.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var appPassword = _config["EmailSettings:AppPassword"];

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
                throw new InvalidOperationException("Thiếu cấu hình EmailSettings:SenderEmail hoặc EmailSettings:AppPassword.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Email người nhận không được để trống.", nameof(toEmail));

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(senderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = body };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(senderEmail, appPassword);

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}
