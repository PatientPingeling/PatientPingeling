using System.Net.Http.Json;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    public sealed class AsyncFlowStatusClient(IHttpClientFactory httpClientFactory) : IAsyncFlowStatusClient
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("AsyncFlowStatus");

        public async Task<AsyncFlowMessageStatus?> GetStatusAsync(string trackingId, CancellationToken ct = default)
        {
            using var response = await _client.GetAsync($"asyncflow/{trackingId}", ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<AsyncFlowStatusResponse>(ct);
            if (body is null)
                return null;

            return new AsyncFlowMessageStatus(body.TrackingId, body.Status, body.ErrorDetails);
        }
    }
}
