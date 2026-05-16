using NotificationService.Domain.Enums;

namespace NotificationService.Application.Abstractions
{
    public interface IMessageProvider
    {
        IReadOnlySet<MessageFormat> SupportedFormats { get; }

        // recipient can be email, phone number or deviceID depending on format
        Task<string> SendAsync(MessageFormat format, string message, string recipient, IReadOnlyDictionary<string, string> credentials, CancellationToken ct);
    }
}
