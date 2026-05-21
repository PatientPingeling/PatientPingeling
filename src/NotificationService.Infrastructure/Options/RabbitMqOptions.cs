using System.ComponentModel.DataAnnotations;

namespace NotificationService.Infrastructure.Options
{
    public sealed class RabbitMqOptions
    {
        public const string SectionName = "RabbitMQ";
        public const string NotificationQueue = "notification_queue";

        [Required(ErrorMessage = "RabbitMQ Host is required.")]
        public string Host { get; set; } = default!;

        [Required]
        public string Username { get; set; } = default!;

        [Required]
        public string Password { get; set; } = default!;

        [Range(1, 65535)]
        public int Port { get; set; } = 5672;
    }
}