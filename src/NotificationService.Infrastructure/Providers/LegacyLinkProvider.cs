using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
  public class LegacyLinkProvider : IMessageProvider
  {
    public Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
    {
      throw new NotImplementedException();
    }
  }
}