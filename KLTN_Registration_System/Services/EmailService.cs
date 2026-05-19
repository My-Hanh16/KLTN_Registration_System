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
            // Thêm dòng này để debug
            Console.WriteLine($"[DEBUG] Đang chuẩn bị gửi mail đến: {toEmail}...");

            var senderEmail = _config["EmailSettings:SenderEmail"];
            var appPassword = _config["EmailSettings:AppPassword"];

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(senderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = body };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            // Thêm dòng này để bỏ qua kiểm tra chứng chỉ lỗi
            smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(senderEmail, appPassword);

            await smtp.SendAsync(email);

            // Thêm dòng này để xác nhận
            Console.WriteLine($"[DEBUG] ===> ĐÃ GỬI THÀNH CÔNG ĐẾN: {toEmail}");

            await smtp.DisconnectAsync(true);
        }
    }
}