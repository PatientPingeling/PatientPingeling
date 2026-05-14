using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
    public class LegacyLinkProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("LegacyLink");

        public async Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
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
