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
                        UnitAmountDecimal = order.TotalPrice * 100m,
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
        if (session.PaymentStatus == "paid")
        {
            var orderIdStr = session.Metadata.GetValueOrDefault("orderId");
            if (int.TryParse(orderIdStr, out var orderId))
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Paid;
                    order.PaymentConfirmation = session.PaymentIntentId ?? session.Id;
                    await _context.SaveChangesAsync();
                }
            }
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
                if (stripeEvent.Data.Object is Session session)
                {
                    await HandleSuccessAsync(session.Id);
                }
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

