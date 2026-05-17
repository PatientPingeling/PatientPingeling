namespace NotificationService.Infrastructure.Providers.SecurePost
{
    internal sealed record SecurePostAuthRequest(string ClientId, string ClientSecret);
    internal sealed record SecurePostAuthResponse(string AccessToken, string TokenType, int ExpiresIn, DateTimeOffset IssuedAt);

    internal sealed record SecurePostMessageRequest(string Format, string Recipient, string Body, string? Subject = "");
    internal sealed record SecurePostMessageResponse(bool Delivered, string TrackingId, string? ErrorMessage, DateTimeOffset DeliveryTimestamp);
}
