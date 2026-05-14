using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
    public class AsyncFlowProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("AsyncFlow");

        public async Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // POST /asyncflow
            // Header: X-API-KEY from credentials["ApiKey"]
            // Body: { "destination": recipient, "content": message, "priority": "normal" }
            // Returns: trackingId from response (format: ASF-...)
            throw new NotImplementedException();
        }
    }
}
