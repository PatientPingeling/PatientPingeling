using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    internal sealed record AsyncFlowRequest(
        [property: JsonPropertyName("destination")] string Destination,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("priority")] string Priority);

    internal sealed record AsyncFlowResponse(
        bool Accepted,
        string TrackingId,
        string Message,
        DateTime SubmittedAt);
}