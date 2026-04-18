namespace RecouvrementAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendWelcomeEmailAsync(string to, string clientName, string token);
        Task SendRenewalEmailAsync(string to, string clientName, string token);
    }
}
