using System.Security.Cryptography;
using System.Text;

namespace BoardWalk.Api.Services.Common
{
    public static class PasswordResetTokenHelper
    {
        /// <summary>Generates a raw, random, URL-safe token to email to the user.</summary>
        public static string GenerateRawToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('='); // URL-safe Base64
        }

        /// <summary>Hashes a raw token for storage/comparison. Fast hash is fine — token is already high-entropy, not a guessable password.</summary>
        public static string Hash(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToBase64String(bytes);
        }
    }
}