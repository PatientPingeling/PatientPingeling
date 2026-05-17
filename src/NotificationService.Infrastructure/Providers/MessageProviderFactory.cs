using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Providers
{
    public class MessageProviderFactory(IServiceProvider sp) : IMessageProviderFactory
    {
        public IMessageProvider Create(string providerName)
        {
            return sp.GetRequiredKeyedService<IMessageProvider>(providerName);
        }
    }
}