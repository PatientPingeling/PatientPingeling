using System.Security.Cryptography;
using System.Text;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Encryption
{
    public sealed class AesGcmEncryptionService(byte[] secretKey) : IEncryptionService
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly byte[] _key = secretKey;

        public string Encrypt(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            var result = new byte[NonceSize + TagSize + ciphertext.Length];

            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string encryptedText)
        {
            ArgumentNullException.ThrowIfNull(encryptedText);

            var fullData = Convert.FromBase64String(encryptedText);
            if (fullData.Length < NonceSize + TagSize)
            {
                throw new CryptographicException("Invalid encrypted payload.");
            }

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[fullData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(fullData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(fullData, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(fullData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            var plaintextBytes = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}