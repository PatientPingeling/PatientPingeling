using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
    public class SwiftSendProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("SwiftSend");

        public async Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // POST /swiftsend
            // Header: X-API-KEY from credentials["ApiKey"]
            // Body: { "type": "SMS"|"EMAIL", "recipients": [recipient], "content": message }
            // Returns: messageId from response
            throw new NotImplementedException();
        }
    }
}
