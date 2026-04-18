using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace RecouvrementAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var smtpHost = _configuration["EmailSettings:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var smtpUser = _configuration["EmailSettings:Username"];
                var smtpPass = _configuration["EmailSettings:Password"];
                var fromAddress = _configuration["EmailSettings:FromAddress"] ?? smtpUser;

                if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                {
                    _logger.LogWarning("Email sending failed: SMTP credentials are not configured in appsettings.json.");
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Recipient}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending email to {Recipient}", to);
                // On ne lève pas l'exception pour ne pas bloquer le flux principal, 
                // mais on loggue l'erreur pour le debug.
            }
        }

        public async Task SendWelcomeEmailAsync(string to, string clientName, string token)
        {
            string lien = $"https://stbbank.tn/portail/{token}";
            string subject = "Bienvenue chez STB BANK - Votre Espace Client";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #004a99;'>Bienvenue chez STB BANK</h2>
                    <p>Bonjour <strong>{clientName}</strong>,</p>
                    <p>Votre compte a été créé avec succès dans notre système de recouvrement.</p>
                    <p>Vous pouvez accéder à votre dossier sécurisé en cliquant sur le bouton ci-dessous :</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{lien}' style='background-color: #004a99; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Accéder à mon espace</a>
                    </div>
                    <p style='color: #666; font-size: 13px;'>Ce lien est strictement personnel et valable pendant 7 jours.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #999; text-align: center;'>Ceci est un message automatique, merci de ne pas y répondre.</p>
                </div>";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendRenewalEmailAsync(string to, string clientName, string token)
        {
            string lien = $"https://stbbank.tn/portail/{token}";
            string subject = "Renouvellement de votre accès STB BANK";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #004a99;'>Accès Renouvelé</h2>
                    <p>Bonjour <strong>{clientName}</strong>,</p>
                    <p>Votre lien d'accès à l'espace client STB a été renouvelé par votre conseiller.</p>
                    <p>Pour consulter l'état de vos dossiers, cliquez sur le lien ci-dessous :</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{lien}' style='background-color: #004a99; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Consulter mon dossier</a>
                    </div>
                    <p style='color: #666; font-size: 13px;'>Ce lien est valable pendant 7 jours.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #999; text-align: center;'>STB BANK - Votre partenaire de réussite.</p>
                </div>";

            await SendEmailAsync(to, subject, body);
        }
    }
}
