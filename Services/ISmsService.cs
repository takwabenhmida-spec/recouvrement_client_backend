namespace RecouvrementAPI.Services
{
    public interface ISmsService
    {
        Task SendSmsAsync(string phoneNumber, string message);
        Task SendTokenSmsAsync(string phoneNumber, string clientName, string token);
    }
}
