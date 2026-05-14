namespace NotificationService.Application.Abstractions
{
    public interface IMessageProviderFactory
    {
        IMessageProvider Create(string providerName);
    }
}