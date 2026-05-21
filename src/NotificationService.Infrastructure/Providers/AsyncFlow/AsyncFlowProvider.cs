using System.Net.Http.Json;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    public sealed class AsyncFlowProvider(
        IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client =
            httpClientFactory.CreateClient("AsyncFlow");

        public IReadOnlySet<MessageFormat> SupportedFormats { get; } =
            new HashSet<MessageFormat>
            {
                MessageFormat.Sms,
                MessageFormat.Email,
                MessageFormat.Push
            };

        public async Task<string> SendAsync(
            MessageFormat format,
            string message,
            string recipient,
            IReadOnlyDictionary<string, string> credentials,
            CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

            if (!credentials.TryGetValue("ApiKey", out var apiKey))
            {
                throw new ArgumentException("Missing credential: ApiKey");
            }

            var request = new AsyncFlowRequest(recipient, message, "normal");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/asyncflow");
            httpRequest.Headers.Add("X-API-KEY", apiKey);
            httpRequest.Content = JsonContent.Create(request);

            using var response = await _client.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<AsyncFlowResponse>(ct) ?? throw new InvalidOperationException("AsyncFlow response was empty.");
            if (!responseBody.Accepted)
            {
                throw new InvalidOperationException("AsyncFlow did not accept the message.");
            }
            if (string.IsNullOrWhiteSpace(responseBody.TrackingId))
            {
                throw new InvalidOperationException("AsyncFlow response did not contain a tracking ID.");
            }

            return responseBody.TrackingId;
        }
    }
}