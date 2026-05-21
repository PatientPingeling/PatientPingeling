using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Providers.SecurePost
{
    internal sealed record SecurePostAuthRequest(
        [property: JsonPropertyName("clientId")] string ClientId,
        [property: JsonPropertyName("clientSecret")] string ClientSecret);

    internal sealed record SecurePostAuthResponse(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("tokenType")] string TokenType,
        [property: JsonPropertyName("expiresIn")] int ExpiresIn,
        [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt);

    internal sealed record SecurePostMessageRequest(
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("recipient")] string Recipient,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("subject")] string? Subject = "");

    internal sealed record SecurePostMessageResponse(
        [property: JsonPropertyName("delivered")] bool Delivered,
        [property: JsonPropertyName("trackingId")] string TrackingId,
        [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
        [property: JsonPropertyName("deliveryTimestamp")] DateTimeOffset DeliveryTimestamp);
}
