using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SysJaky_N.Authorization;
using SysJaky_N.Models;
using SysJaky_N.Services;
using WebPush;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("push")]
public class PushController : ControllerBase
{
    private readonly IPushSubscriptionStore _subscriptionStore;
    private readonly ILogger<PushController> _logger;
    private readonly PushNotificationOptions _options;

    public PushController(
        IPushSubscriptionStore subscriptionStore,
        IOptions<PushNotificationOptions> options,
        ILogger<PushController> logger)
    {
        _subscriptionStore = subscriptionStore;
        _logger = logger;
        _options = options.Value ?? new PushNotificationOptions();
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { message = "Subscription endpoint is required." });
        }

        if (request.Keys is null || string.IsNullOrWhiteSpace(request.Keys.P256dh) || string.IsNullOrWhiteSpace(request.Keys.Auth))
        {
            return BadRequest(new { message = "Invalid subscription keys." });
        }

        var topics = request.Topics?.Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var record = new PushSubscriptionRecord
        {
            Endpoint = request.Endpoint,
            P256dh = request.Keys.P256dh,
            Auth = request.Keys.Auth,
            Topics = topics
        };

        await _subscriptionStore.SaveAsync(record, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { message = "Subscription endpoint is required." });
        }

        await _subscriptionStore.RemoveAsync(request.Endpoint, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("notify")]
    [Authorize(Policy = AuthorizationPolicies.AdminDashboardAccess)]
    public async Task<IActionResult> Notify([FromBody] PushMessageRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Notification payload is required." });
        }

        if (string.IsNullOrWhiteSpace(_options.PublicKey) || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Push notifications are not configured." });
        }

        var client = new WebPushClient();
        var vapidDetails = new VapidDetails(
            string.IsNullOrWhiteSpace(_options.Subject) ? "mailto:admin@example.com" : _options.Subject,
            _options.PublicKey,
            _options.PrivateKey);

        var subscriptions = string.IsNullOrWhiteSpace(request.Topic)
            ? await _subscriptionStore.GetAllAsync(cancellationToken)
            : await _subscriptionStore.GetByTopicAsync(request.Topic, cancellationToken);

        if (subscriptions.Count == 0)
        {
            return Ok(new { success = true, message = "No subscribers for the selected topic." });
        }

        var payload = JsonSerializer.Serialize(new
        {
            title = string.IsNullOrWhiteSpace(request.Title) ? "SysJaky" : request.Title,
            body = request.Body,
            url = string.IsNullOrWhiteSpace(request.Url) ? "/" : request.Url,
            tag = request.Topic,
            actions = new[]
            {
                new { action = "open", title = "Otevřít" }
            }
        });

        var failed = new List<string>();

        foreach (var subscription in subscriptions)
        {
            var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

            try
            {
                await client.SendNotificationAsync(pushSubscription, payload, vapidDetails, cancellationToken);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Removing expired push subscription for {Endpoint}", subscription.Endpoint);
                await _subscriptionStore.RemoveAsync(subscription.Endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send push notification to {Endpoint}", subscription.Endpoint);
                failed.Add(subscription.Endpoint);
            }
        }

        return Ok(new { success = true, failed });
    }

    public class PushSubscriptionRequest
    {
        public string? Endpoint { get; set; }
        public PushSubscriptionKeys? Keys { get; set; }
        public IEnumerable<string>? Topics { get; set; }
    }

    public class PushSubscriptionKeys
    {
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }

    public class PushUnsubscribeRequest
    {
        public string? Endpoint { get; set; }
    }

    public class PushMessageRequest
    {
        public string? Title { get; set; }
        public required string Body { get; set; }
        public string? Url { get; set; }
        public string? Topic { get; set; }
    }
}
