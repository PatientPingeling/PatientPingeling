using System.Security.Cryptography;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Security
{
    public sealed class Pbkdf2HashingService : IHashingService
    {
        private const int SaltSize       = 32;
        private const int HashSize       = 32;
        private const int Iterations     = 100_000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        // Stored format: "{base64salt}:{base64hash}"
        public string Hash(string plainText)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, Algorithm, HashSize);
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public bool Validate(string storedHash, string plainText)
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expected = Convert.FromBase64String(parts[1]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, Algorithm, HashSize);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
    }
}
