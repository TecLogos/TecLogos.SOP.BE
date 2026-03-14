using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using TecLogos.SOP.DataModel.Auth;


namespace TecLogos.SOP.BAL.Auth
{
    public interface IEmailBAL
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailBAL : IEmailBAL
    {
        private readonly EmailSettings _settings;

        public EmailBAL(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpClient = new SmtpClient(_settings.Host)
            {
                Port = _settings.Port,
                Credentials = new NetworkCredential(
                    _settings.UserName,
                    _settings.Password),
                EnableSsl = _settings.EnableSSL
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}