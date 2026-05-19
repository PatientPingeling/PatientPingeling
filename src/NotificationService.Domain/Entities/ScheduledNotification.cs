namespace NotificationService.Domain.Entities
{
    public sealed class ScheduledNotification
    {
        public Guid Id { get; set; }
        public DateTimeOffset SendAt { get; set; }

        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;
    }
}
