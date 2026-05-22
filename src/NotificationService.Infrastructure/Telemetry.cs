using System.Diagnostics;

namespace NotificationService.Infrastructure
{
    public static class Telemetry
    {
        public const string ActivitySourceName = "NotificationService";
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    }
}