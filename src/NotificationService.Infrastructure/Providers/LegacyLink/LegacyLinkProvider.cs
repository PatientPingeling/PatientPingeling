using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.LegacyLink
{
    public class LegacyLinkProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("LegacyLink");

        public IReadOnlySet<MessageFormat> SupportedFormats { get; } =
            new HashSet<MessageFormat> { MessageFormat.Sms };

        public Task<string> SendAsync(MessageFormat format, string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // POST /LegacyLink/SendSms
            // Header: Authorization: Basic base64(credentials["Username"]:credentials["Password"])
            // Content-Type: application/xml
            // Body: <SendSmsRequest> with PhoneNumber, MessageText, SenderIdentification
            // Returns: MessageReference from XML response
            throw new NotImplementedException();
        }
    }
}
