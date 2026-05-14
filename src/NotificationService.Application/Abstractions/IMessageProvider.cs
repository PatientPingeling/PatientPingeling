namespace NotificationService.Application.Abstractions
{
    public interface IMessageProvider
    {
        //recipient can be email, phone number of deviceID
        Task<string> SendMessageAsync(string recipient, string message, CancellationToken cancellationToken);
    }
}
