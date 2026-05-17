using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.SecurePost;

public sealed class SecurePostProvider(IHttpClientFactory httpClientFactory, IMemoryCache cache) : IMessageProvider
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("SecurePost");
    private readonly IMemoryCache _cache = cache;

    public IReadOnlySet<MessageFormat> SupportedFormats =>
        new HashSet<MessageFormat>
        {
            MessageFormat.Email,
            MessageFormat.Sms,
            MessageFormat.Push
        };

    public async Task<string> SendAsync(MessageFormat format, string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct)
    {
        var subject = format == MessageFormat.Email ? "Afspraak herinnering" : "Herinnering";

        if (!credentials.TryGetValue("ClientId", out var clientId))
        {
            throw new InvalidOperationException("SecurePost ClientId is missing.");
        }
        if (!credentials.TryGetValue("ClientSecret", out var clientSecret))
        {
            throw new InvalidOperationException("SecurePost ClientSecret is missing.");
        }

        var authResult = await AuthenticateAsync(clientId, clientSecret, ct);

        //! Send Message
        using var request = new HttpRequestMessage(HttpMethod.Post, "message");
        request.Headers.Authorization = new AuthenticationHeaderValue(authResult.TokenType, authResult.AccessToken);
        request.Content = JsonContent.Create(new SecurePostMessageRequest(MapFormat(format), recipient, message, subject));

        var response = await _client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode is false)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"SecurePost /message returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<SecurePostMessageResponse>(ct)
            ?? throw new InvalidOperationException("SecurePost message response was empty.");

        return result.TrackingId;
    }


    // IMemoryCache stores objects directly in memory
    // Downside: not distributed. If multiple instances run, each has its own cache and will call /auth independently. 
    // TODO: Switch to IDistributedCache + Redis if we are gonna use multiple instances.
    private async Task<SecurePostAuthResponse> AuthenticateAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        var cacheKey = $"auth_securepost_{clientId}";
        const int expiresBuffer = 30; // int in seconds buffer

        //* Cache hit — return early
        if (_cache.TryGetValue(cacheKey, out SecurePostAuthResponse? cached))
        {
            return cached!;
        }

        //* Cache miss — call auth
        var response = await _client.PostAsJsonAsync("auth", new SecurePostAuthRequest(clientId, clientSecret), ct);
        response.EnsureSuccessStatusCode();

        var authResult = await response.Content.ReadFromJsonAsync<SecurePostAuthResponse>(ct)
            ?? throw new InvalidOperationException("SecurePost auth response was empty.");

        var expiresIn = Math.Max(authResult.ExpiresIn - expiresBuffer, 30);

        _cache.Set(cacheKey, authResult, TimeSpan.FromSeconds(expiresIn));

        return authResult;
    }

    private static string MapFormat(MessageFormat format) => format switch
    {
        MessageFormat.Email => "EMAIL",
        MessageFormat.Sms => "SMS",
        MessageFormat.Push => "PUSH",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}