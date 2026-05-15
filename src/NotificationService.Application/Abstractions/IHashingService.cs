namespace NotificationService.Application.Abstractions
{
  public interface IHashingService
  {
    string Hash(string plainText);
    bool Validate(string hashedValue, string plainText);
  }
}