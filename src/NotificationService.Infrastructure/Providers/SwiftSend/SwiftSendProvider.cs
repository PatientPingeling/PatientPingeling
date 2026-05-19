using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.SwiftSend
{
    public class SwiftSendProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client =
            httpClientFactory.CreateClient("SwiftSend");

        public IReadOnlySet<MessageFormat> SupportedFormats { get; } =
            new HashSet<MessageFormat>
            {
                MessageFormat.Sms,
                MessageFormat.Email
            };

        public async Task<string> SendAsync(
            MessageFormat format,
            string message,
            string recipient,
            IReadOnlyDictionary<string, string> credentials,
            CancellationToken ct)
        {
            if (!credentials.TryGetValue("ApiKey", out var apiKey))
            {
                throw new InvalidOperationException(
                    "Missing ApiKey credential"
                );
            }

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/swiftsend"
            );

            request.Headers.Add("X-API-KEY", apiKey);

            request.Content = JsonContent.Create(new
            {
                type = format == MessageFormat.Sms
                    ? "SMS"
                    : "EMAIL",

                recipients = new[] { recipient },

                content = message
            });

            var response = await _client.SendAsync(request, ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                throw new HttpRequestException(
                    $"SwiftSend failed with status {(int)response.StatusCode}: {body}"
                );
            }

            var result = JsonSerializer.Deserialize<SwiftSendResponse>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (result is null)
            {
                throw new InvalidOperationException(
                    "SwiftSend returned invalid JSON"
                );
            }

            if (string.IsNullOrWhiteSpace(result.MessageId))
            {
                throw new InvalidOperationException(
                    "SwiftSend response missing messageId"
                );
            }

            return result.MessageId;
        }

        private sealed class SwiftSendResponse
        {
            public bool Success { get; set; }

            public string MessageId { get; set; } = string.Empty;

            public string[] FailedRecipients { get; set; } = [];

            public string? Error { get; set; }
        }
    }
}
