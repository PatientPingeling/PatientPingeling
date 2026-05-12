namespace NotificationService.Domain.Entities
{
    public sealed class Patient
    {
        public int Id { get; set; }
        public string GivenName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public ICollection<Appointment> Appointments { get; set; } = [];
    }
}
