using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BoardWalk.Api.Services.Auth
{
    public static class BotAuth
    {
        public const string Scheme = "BotKey";
        public const string KeyHeader = "X-Bot-Key";
        public const string ActAsHeader = "X-Act-As";
        public const string ActorClaim = "actor";
        public const string BotActor = "bot";
    }

    /// <summary>
    /// Authenticates the bot SERVICE (not a person). The bot sends a shared secret in X-Bot-Key, and names the seat it is
    /// playing for in X-Act-As. The handler only proves "this is the bot service"; WHICH seats it may act for is enforced
    /// by the game itself (it only finds seats whose control is Bot), so a leaked key still cannot move a human's pieces.
    /// </summary>
    public class BotKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IConfiguration _config;

        public BotKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration config) : base(options, logger, encoder)
        {
            _config = config;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(BotAuth.KeyHeader, out var supplied))
                return Task.FromResult(AuthenticateResult.NoResult()); // not a bot request: let other schemes try

            var expected = _config["Bot:ApiKey"];
            if (string.IsNullOrEmpty(expected))
                return Task.FromResult(AuthenticateResult.Fail("Bot service access is not configured."));

            // Constant-time comparison, so response timing cannot be used to guess the key byte by byte.
            var ok = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(supplied.ToString()),
                Encoding.UTF8.GetBytes(expected));
            if (!ok)
                return Task.FromResult(AuthenticateResult.Fail("Invalid bot key."));

            var claims = new List<Claim> { new(BotAuth.ActorClaim, BotAuth.BotActor) };

            if (Request.Headers.TryGetValue(BotAuth.ActAsHeader, out var actAs))
            {
                if (!Guid.TryParse(actAs, out var seatUserId))
                    return Task.FromResult(AuthenticateResult.Fail("X-Act-As must be a user id."));

                // "sub" is what every controller reads as the current user — the bot simply "is" that seat for this request.
                claims.Add(new Claim("sub", seatUserId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, BotAuth.Scheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), BotAuth.Scheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
