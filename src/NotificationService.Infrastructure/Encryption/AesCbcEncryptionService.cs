using System.Security.Cryptography;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Encryption
{
    public sealed class AesCbcEncryptionService() : IEncryptionService
    {
        private const int iVSize = 16;
        private const CipherMode cipherMode = CipherMode.CBC;
        private const PaddingMode paddingMode = PaddingMode.PKCS7;

        public string Encrypt(string plainText, byte[] masterKey)
        {
            using var aes = Aes.Create();
            aes.Key = masterKey;
            aes.Mode = cipherMode;
            aes.Padding = paddingMode;
            aes.IV = RandomNumberGenerator.GetBytes(iVSize);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(aes.IV, 0, iVSize);

            using var encryptor = aes.CreateEncryptor();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            using var streamWriter = new StreamWriter(cryptoStream);
            streamWriter.Write(plainText);
            streamWriter.Flush();
            cryptoStream.FlushFinalBlock();

            return Convert.ToBase64String(memoryStream.ToArray());
        }
        public string Decrypt(string cipherText, byte[] masterKey)
        {
            var cipherData = Convert.FromBase64String(cipherText);
            if (cipherData.Length < iVSize)
            {
                throw new Exception();
            }

            var iv = new byte[iVSize];
            var encryptedData = new byte[cipherData.Length - iVSize];

            Buffer.BlockCopy(cipherData, 0, iv, 0, iVSize);
            Buffer.BlockCopy(cipherData, iVSize, encryptedData, 0, encryptedData.Length);

            using var aes = Aes.Create();
            aes.Key = masterKey;
            aes.Mode = cipherMode;
            aes.Padding = paddingMode;
            aes.IV = iv;

            using var memoryStream = new MemoryStream(encryptedData);
            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);

            return streamReader.ReadToEnd();
        }
    }
}