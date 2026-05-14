using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
  public class AsyncFlowProvider : IMessageProvider
  {
    public Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
    {
      throw new NotImplementedException();
    }
  }
}