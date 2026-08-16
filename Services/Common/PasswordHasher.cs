using System.Security.Cryptography;

namespace BoardWalk.Api.Services.Common
{
    public static class PasswordHasher
    {
        // These are tuning constants for PBKDF2:
        private const int SaltSize = 16;       // 16 bytes = 128 bits of randomness
        private const int HashSize = 32;       // 32 bytes = 256 bits, output length
        private const int Iterations = 100_000; // how many times the hash loop runs
        // (100,000 is a widely recommended minimum as of current OWASP guidance)

        // Generates a random salt, unique every single call.
        public static string GenerateSalt()
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            return Convert.ToBase64String(saltBytes);
            // Base64 turns raw bytes into a plain text string, safe to store
            // in a text column in Postgres (e.g. "x7Ga9k2LpQ==")
        }

        // Combines password + salt and runs it through PBKDF2.
        public static string HashPassword(string password, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);

            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: saltBytes,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );

            return Convert.ToBase64String(hashBytes);
        }

        // Used at login time: re-hash the entered password with the SAME salt,
        // and check if it matches the stored hash. We never "decrypt" the hash —
        // we just recompute and compare.
        public static bool VerifyPassword(string password, string salt, string storedHash)
        {
            string computedHash = HashPassword(password, salt);
            return computedHash == storedHash;
        }
    }
}
