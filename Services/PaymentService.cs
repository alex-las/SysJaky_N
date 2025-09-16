using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class PaymentGatewayOptions
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public class PaymentService
{
    private readonly PaymentGatewayOptions _options;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public bool IsEnabled => _options.Enabled;

    public PaymentService(IOptions<PaymentGatewayOptions> options, ApplicationDbContext context, ILogger<PaymentService> logger)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;

        if (_options.Enabled)
            StripeConfiguration.ApiKey = _options.ApiKey;
    }

    public async Task<string?> CreatePaymentAsync(Order order, string successUrl, string cancelUrl)
    {
        if (!_options.Enabled)
            return null;

        var service = new SessionService();
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "czk",
                        UnitAmountDecimal = order.Total * 100m,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order {order.Id}"
                        }
                    },
                    Quantity = 1
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "orderId", order.Id.ToString() }
            }
        };

        var session = await service.CreateAsync(sessionOptions);
        order.PaymentConfirmation = session.Id;
        await _context.SaveChangesAsync();
        return session.Url;
    }

    public async Task HandleSuccessAsync(string sessionId)
    {
        if (!_options.Enabled)
            return;

        var service = new SessionService();
        var session = await service.GetAsync(sessionId);
        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            return;

        var orderIdStr = session.Metadata.GetValueOrDefault("orderId");
        if (!int.TryParse(orderIdStr, out var orderId))
            return;

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return;

        var paymentConfirmation = session.PaymentIntentId ?? session.Id;
        if (order.Status == OrderStatus.Paid && !string.IsNullOrEmpty(order.PaymentConfirmation))
        {
            if (!string.Equals(order.PaymentConfirmation, paymentConfirmation, StringComparison.Ordinal))
            {
                order.PaymentConfirmation = paymentConfirmation;
                await _context.SaveChangesAsync();
            }
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Paid;
            order.PaymentConfirmation = paymentConfirmation;

            if (!string.IsNullOrEmpty(order.UserId) && order.Items.Count > 0)
            {
                var courseIds = order.Items.Select(i => i.CourseId).Distinct().ToList();

                var terms = await _context.CourseTerms
                    .Where(term => courseIds.Contains(term.CourseId) && term.IsActive)
                    .OrderBy(term => term.StartUtc)
                    .ToListAsync();

                var termLookup = terms
                    .GroupBy(term => term.CourseId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                foreach (var item in order.Items)
                {
                    if (item.Quantity <= 0)
                        continue;

                    if (!termLookup.TryGetValue(item.CourseId, out var courseTerms) || courseTerms.Count == 0)
                    {
                        _logger.LogWarning(
                            "No active course terms available for course {CourseId} when processing order {OrderId}.",
                            item.CourseId,
                            order.Id);
                        continue;
                    }

                    var seatsNeeded = item.Quantity;
                    foreach (var term in courseTerms)
                    {
                        var availableSeats = term.Capacity - term.SeatsTaken;
                        if (availableSeats <= 0)
                            continue;

                        var seatsToAllocate = Math.Min(seatsNeeded, availableSeats);
                        term.SeatsTaken += seatsToAllocate;

                        for (var i = 0; i < seatsToAllocate; i++)
                        {
                            _context.Enrollments.Add(new Enrollment
                            {
                                UserId = order.UserId!,
                                CourseTermId = term.Id,
                                Status = EnrollmentStatus.Confirmed
                            });
                        }

                        seatsNeeded -= seatsToAllocate;
                        if (seatsNeeded <= 0)
                            break;
                    }

                    if (seatsNeeded > 0)
                    {
                        _logger.LogWarning(
                            "Could not allocate {RemainingSeats} seats for course {CourseId} in order {OrderId} due to limited capacity.",
                            seatsNeeded,
                            item.CourseId,
                            order.Id);
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing payment success for order {OrderId}.", orderId);
            throw;
        }
    }

    public async Task HandleWebhookAsync(HttpRequest request)
    {
        if (!_options.Enabled)
            return;

        var json = await new StreamReader(request.Body).ReadToEndAsync();
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, request.Headers["Stripe-Signature"], _options.WebhookSecret);
            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var alreadyProcessed = await _context.PaymentIds.AnyAsync(p => p.Id == stripeEvent.Id);
                if (alreadyProcessed)
                {
                    return;
                }

                _context.PaymentIds.Add(new PaymentId
                {
                    Id = stripeEvent.Id,
                    ProcessedUtc = DateTime.UtcNow
                });

                if (stripeEvent.Data.Object is Session session)
                {
                    await HandleSuccessAsync(session.Id);
                }

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling payment webhook. Signature: {Signature}, Payload: {Payload}",
                request.Headers["Stripe-Signature"], json);
        }
    }
}

