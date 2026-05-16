using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    public class AsyncFlowProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("AsyncFlow");

        public IReadOnlySet<MessageFormat> SupportedFormats => throw new NotImplementedException();

        public Task<string> SendAsync(MessageFormat format, string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // POST /asyncflow
            // Header: X-API-KEY from credentials["ApiKey"]
            // Body: { "destination": recipient, "content": message, "priority": "normal" }
            // Returns: trackingId from response (format: ASF-...)
            throw new NotImplementedException();
        }
    }
}
