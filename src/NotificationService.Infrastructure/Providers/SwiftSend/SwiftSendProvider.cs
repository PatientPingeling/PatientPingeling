using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.SwiftSend
{
    public class SwiftSendProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("SwiftSend");

        public IReadOnlySet<MessageFormat> SupportedFormats { get; } =
            new HashSet<MessageFormat> { MessageFormat.Sms, MessageFormat.Email };

        public Task<string> SendAsync(MessageFormat format, string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // POST /swiftsend
            // Header: X-API-KEY from credentials["ApiKey"]
            // Body: { "type": "SMS"|"EMAIL", "recipients": [recipient], "content": message }
            // Returns: messageId from response
            throw new NotImplementedException();
        }
    }
}
