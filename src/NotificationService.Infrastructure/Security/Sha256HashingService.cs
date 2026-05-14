using System.Security.Cryptography;
using System.Text;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Security
{
    public class Sha256HashingService : IHashingService
    {
        public string Hash(string plainText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            byte[] hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash).ToLower();
        }

        public bool Validate(string hashedValue, string plainText)
        {
            string computedHash = Hash(plainText);

            // constant-time compare
            byte[] a = Convert.FromHexString(hashedValue);
            byte[] b = Convert.FromHexString(computedHash);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}