namespace NotificationService.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");

        group.MapGet("/", () => Results.Ok(new { message = "Notification service is running." }))
            .WithName("GetNotifications");

        return app;
    }
}
