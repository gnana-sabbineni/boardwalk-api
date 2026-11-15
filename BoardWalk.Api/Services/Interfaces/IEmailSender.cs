namespace BoardWalk.Api.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    }
}