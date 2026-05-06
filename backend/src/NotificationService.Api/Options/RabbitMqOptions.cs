namespace NotificationService.Api.Options
{
  public sealed class RabbitMqOptions
  {
    public string Host { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public int Port { get; set; }
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "notification.events";
  }
}