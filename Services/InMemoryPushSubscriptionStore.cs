using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class InMemoryPushSubscriptionStore : IPushSubscriptionStore
{
    private readonly ConcurrentDictionary<string, PushSubscriptionRecord> _subscriptions = new(StringComparer.Ordinal);

    public Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken cancellationToken = default)
    {
        if (subscription is null)
        {
            throw new ArgumentNullException(nameof(subscription));
        }

        var cloned = new PushSubscriptionRecord
        {
            Endpoint = subscription.Endpoint,
            P256dh = subscription.P256dh,
            Auth = subscription.Auth,
            Topics = new HashSet<string>(subscription.Topics, StringComparer.OrdinalIgnoreCase)
        };

        _subscriptions.AddOrUpdate(subscription.Endpoint, cloned, (_, _) => cloned);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<PushSubscriptionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PushSubscriptionRecord> items = _subscriptions.Values
            .Select(Clone)
            .ToArray();
        return Task.FromResult(items);
    }

    public Task<IReadOnlyCollection<PushSubscriptionRecord>> GetByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return GetAllAsync(cancellationToken);
        }

        var filtered = _subscriptions.Values
            .Where(subscription => subscription.Topics.Contains(topic, StringComparer.OrdinalIgnoreCase) || subscription.Topics.Count == 0)
            .Select(Clone)
            .ToArray();

        IReadOnlyCollection<PushSubscriptionRecord> result = filtered;
        return Task.FromResult(result);
    }

    public Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Task.CompletedTask;
        }

        _subscriptions.TryRemove(endpoint, out _);
        return Task.CompletedTask;
    }

    private static PushSubscriptionRecord Clone(PushSubscriptionRecord subscription) => new()
    {
        Endpoint = subscription.Endpoint,
        P256dh = subscription.P256dh,
        Auth = subscription.Auth,
        Topics = new HashSet<string>(subscription.Topics, StringComparer.OrdinalIgnoreCase)
    };
}
