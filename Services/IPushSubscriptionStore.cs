using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface IPushSubscriptionStore
{
    Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PushSubscriptionRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PushSubscriptionRecord>> GetByTopicAsync(string topic, CancellationToken cancellationToken = default);
    Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default);
}
