namespace NotificationService.Application.Interfaces
{
  public interface IEncryptionService
  {
    string Encrypt(string plainText, byte[] masterKey);
    string Decrypt(string cipherText, byte[] masterKey);
  }
}