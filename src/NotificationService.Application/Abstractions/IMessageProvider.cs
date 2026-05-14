namespace NotificationService.Application.Abstractions
{
    public interface IMessageProvider
    {
        //recipient can be email, phone number of deviceID
        Task<string> SendAsync(string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct);
    }
}
