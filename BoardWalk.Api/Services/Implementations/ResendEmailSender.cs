using Resend;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class ResendEmailSender : IEmailSender
    {
        private readonly IResend _resend;
        private readonly IConfiguration _config;
        private readonly ILogger<ResendEmailSender> _logger;

        public ResendEmailSender(IResend resend, IConfiguration config, ILogger<ResendEmailSender> logger)
        {
            _resend = resend;
            _config = config;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            var message = new EmailMessage
            {
                From = _config["Resend:FromAddress"],
                To = "gnanavardhan176@gmail.com",
                Subject = "Reset your BoardWalk password",
                HtmlBody = $"""
                    <p>Click the link below to reset your password. This link expires in 30 minutes.</p>
                    <p><a href="{resetLink}">{resetLink}</a></p>
                    <p>If you didn't request this, you can safely ignore this email.</p>
                    """
            };

            var response = await _resend.EmailSendAsync(message);

            if (!response.Success)
            {
                _logger.LogError("Resend email failed: {Error}", response.Content);
                // Still not throwing — see prior explanation on why forgot-password must not leak failure state.
            }
        }
    }
}