using Microsoft.Extensions.Logging;

namespace RecouvrementAPI.Services
{
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;

        public SmsService(ILogger<SmsService> logger)
        {
            _logger = logger;
        }

        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            // Simulation d'envoi de SMS. 
            // Pour une intégration réelle, utiliser HttpClient pour appeler une API (ex: Twilio, Ooredoo, Orange, etc.)
            
            _logger.LogInformation("--- ENVOI SMS RÉEL (SIMULATION LOG) ---");
            _logger.LogInformation("Destinataire : {PhoneNumber}", phoneNumber);
            _logger.LogInformation("Message : {Message}", message);
            _logger.LogInformation("----------------------------------------");

            // Simuler un délai réseau
            await Task.Delay(500);
        }

        public async Task SendTokenSmsAsync(string phoneNumber, string clientName, string token)
        {
            string lien = $"https://stbbank.tn/portail/{token}";
            string message = $"[STB BANK] Cher(e) {clientName}, accédez à votre dossier sécurisé via ce lien : {lien} (Valable 7 jours)";

            await SendSmsAsync(phoneNumber, message);
        }
    }
}
