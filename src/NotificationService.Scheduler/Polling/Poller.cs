using System.Threading.Channels;

namespace NotificationService.Scheduler.Polling
{
    public class Poller(
        ILogger<Poller> logger,
        Channel<> channel)
    {
        
    }
}
