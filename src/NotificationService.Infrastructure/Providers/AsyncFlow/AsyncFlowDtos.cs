namespace NotificationService.Infrastructure.Providers.AsyncFlow
{
    public sealed record AsyncFlowRequest(
        string Destination,
        string Content,
        string Priority);

    public sealed record AsyncFlowResponse(
        bool Accepted,
        string TrackingId,
        string Message,
        DateTime SubmittedAt);
}