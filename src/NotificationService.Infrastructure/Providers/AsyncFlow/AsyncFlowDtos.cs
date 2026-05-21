using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    public sealed record AsyncFlowRequest(
        [property: JsonPropertyName("destination")] string Destination,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("priority")] string Priority);

    public sealed record AsyncFlowResponse(
        bool Accepted,
        string TrackingId,
        string Message,
        DateTime SubmittedAt);
}