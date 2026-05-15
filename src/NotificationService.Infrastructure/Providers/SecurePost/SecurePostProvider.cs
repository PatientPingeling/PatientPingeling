using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers.SecurePost
{
    public class SecurePostProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("SecurePost");

        public async Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
        {
            // Step 1: POST /securepost/auth with credentials["ClientId"] + credentials["ClientSecret"]
            //         Cache the JWT token (expires in 3 minutes)
            // Step 2: POST /securepost/message
            //         Header: Authorization: Bearer <token>
            //         Body: { "format": "SMS"|"EMAIL"|"PUSH", "recipient": recipient, "body": message }
            // Returns: trackingId from response
            throw new NotImplementedException();
        }
    }
}
