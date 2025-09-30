using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;
using SysJaky_N.Services;
using SysJaky_N.EmailTemplates.Models;
using EmailTemplate = SysJaky_N.Services.EmailTemplate;

namespace SysJaky_N.Pages;

[Authorize]
public class CartModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;
    private readonly CartService _cartService;
    private readonly IStringLocalizer<CartModel> _localizer;

    private const decimal VatRate = 0.21m;

    public CartModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IAuditService auditService,
        CartService cartService,
        IStringLocalizer<CartModel> localizer)
    {
        _context = context;
        _userManager = userManager;
        _emailSender = emailSender;
        _auditService = auditService;
        _cartService = cartService;
        _localizer = localizer;
    }

    public List<CartItemView> Items { get; set; } = new();
    public decimal Total { get; set; }
    public Voucher? AppliedVoucher { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CourseBlock> BundleOffers { get; } = new();
    public List<CourseBlock> AppliedBundles { get; } = new();

    public async Task OnGetAsync()
    {
        await LoadCartAsync();
        await ApplyStoredVoucherAsync();
    }

    [EnableRateLimiting("Checkout")]
    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        var cart = _cartService.GetItems(HttpContext.Session).ToList();
        if (!cart.Any())
        {
            await LoadCartAsync();
            await ApplyStoredVoucherAsync();
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = _localizer["Empty"];
            }

            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var ids = cart.Select(c => c.CourseId).ToList();
        var pricingContext = await BuildCoursePricingContextAsync(ids);
        var subtotal = await CalculateTotalAsync(cart, pricingContext);
        var cartLines = BuildCartLines(cart, pricingContext);

        Voucher? voucher = null;
        var voucherId = HttpContext.Session.GetInt32("VoucherId");
        if (voucherId.HasValue)
        {
            voucher = await _context.Vouchers.FindAsync(voucherId.Value);
            if (voucher == null || !IsVoucherValidForCart(voucher, cartLines))
            {
                voucher = null;
                HttpContext.Session.Remove("VoucherId");
            }
        }

        var total = Math.Round(Math.Max(subtotal, 0m), 2, MidpointRounding.AwayFromZero);
        if (voucher != null)
        {
            var discount = CalculateVoucherDiscount(total, voucher, cartLines);
            total = Math.Round(Math.Max(total - discount, 0m), 2, MidpointRounding.AwayFromZero);
        }

        var pricing = BuildOrderPricing(cart, pricingContext, total);
        if (!pricing.Items.Any())
        {
            await LoadCartAsync();
            await ApplyStoredVoucherAsync();
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = _localizer["Empty"];
            }

            return Page();
        }

        var order = new Order
        {
            UserId = user.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = pricing.Items,
            PriceExclVat = pricing.PriceExclVat,
            Vat = pricing.Vat,
            Total = total,
            TotalPrice = total,
            VoucherId = voucher?.Id
        };

        _context.Orders.Add(order);
        if (voucher != null)
        {
            voucher.UsedCount += 1;
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync(user.Id, "OrderCreated", $"Order {order.Id} created");
        await _emailSender.SendEmailAsync(
            user.Email!,
            EmailTemplate.OrderCreated,
            new OrderCreatedEmailModel(order.Id, order.Total, order.CreatedAt, user.Email));

        _cartService.Clear(HttpContext.Session);
        HttpContext.Session.Remove("VoucherId");
        HttpContext.Session.Remove("Bundles");

        return Redirect("/Account/Manage#orders");
    }

    public async Task<IActionResult> OnPostApplyVoucherAsync(string code)
    {
        await LoadCartAsync();

        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
        var cartLines = BuildCartLines(Items);
        if (voucher == null || !IsVoucherValidForCart(voucher, cartLines))
        {
            ErrorMessage = _localizer["InvalidVoucher"];
            HttpContext.Session.Remove("VoucherId");
            return Page();
        }

        ErrorMessage = null;
        HttpContext.Session.SetInt32("VoucherId", voucher.Id);
        ApplyVoucher(voucher, cartLines);
        return Page();
    }

    public IActionResult OnPostApplyBundle(int blockId)
    {
        var bundles = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        if (!bundles.Contains(blockId))
        {
            bundles.Add(blockId);
            HttpContext.Session.SetObject("Bundles", bundles);
        }

        return RedirectToPage();
    }

    private async Task LoadCartAsync()
    {
        var cart = _cartService.GetItems(HttpContext.Session).ToList();
        var ids = cart.Select(c => c.CourseId).ToList();
        var pricingContext = await BuildCoursePricingContextAsync(ids);
        var cartLines = BuildCartLines(cart, pricingContext);

        Items = cartLines.Select(line => new CartItemView
        {
            Course = line.Course,
            Quantity = line.Quantity
        }).ToList();

        var bundleIds = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        var blocks = await _context.CourseBlocks
            .AsNoTracking()
            .Include(b => b.Modules)
            .ToListAsync();

        foreach (var block in blocks)
        {
            foreach (var module in block.Modules)
            {
                if (pricingContext.CourseMap.TryGetValue(module.Id, out var moduleCourse))
                {
                    module.Price = moduleCourse.Price;
                }
            }

            var moduleIds = block.Modules.Select(m => m.Id).ToList();
            if (moduleIds.All(id => cart.Any(ci => ci.CourseId == id)))
            {
                if (bundleIds.Contains(block.Id))
                {
                    AppliedBundles.Add(block);
                }
                else
                {
                    BundleOffers.Add(block);
                }
            }
            else
            {
                bundleIds.Remove(block.Id);
            }
        }

        HttpContext.Session.SetObject("Bundles", bundleIds);

        Total = await CalculateTotalAsync(cart, pricingContext);
    }

    private async Task ApplyStoredVoucherAsync()
    {
        var id = HttpContext.Session.GetInt32("VoucherId");
        if (!id.HasValue)
        {
            return;
        }

        var voucher = await _context.Vouchers.FindAsync(id.Value);
        var cartLines = BuildCartLines(Items);
        if (voucher != null && IsVoucherValidForCart(voucher, cartLines))
        {
            ApplyVoucher(voucher, cartLines);
        }
        else
        {
            HttpContext.Session.Remove("VoucherId");
        }
    }

    private void ApplyVoucher(Voucher voucher, IReadOnlyCollection<CartLine> cartLines)
    {
        AppliedVoucher = voucher;
        DiscountAmount = CalculateVoucherDiscount(Total, voucher, cartLines);
        Total = Math.Round(Math.Max(Total - DiscountAmount, 0m), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateVoucherDiscount(decimal total, Voucher voucher, IReadOnlyCollection<CartLine> cartLines)
    {
        if (total <= 0 || cartLines.Count == 0)
        {
            return 0m;
        }

        decimal targetTotal = total;
        if (voucher.AppliesToCourseId.HasValue)
        {
            targetTotal = cartLines
                .Where(line => line.CourseId == voucher.AppliesToCourseId.Value)
                .Sum(line => line.LineTotal);

            if (targetTotal <= 0)
            {
                return 0m;
            }
        }

        var discount = voucher.Type switch
        {
            VoucherType.Percentage => Math.Round(targetTotal * ClampPercentage(voucher.Value) / 100m, 2, MidpointRounding.AwayFromZero),
            VoucherType.FixedAmount => Math.Min(voucher.Value, targetTotal),
            _ => 0m
        };

        return Math.Min(discount, total);
    }

    private static decimal ClampPercentage(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        if (value > 100m)
        {
            return 100m;
        }

        return value;
    }

    private static bool IsVoucherCurrentlyValid(Voucher voucher)
    {
        if (voucher.Value <= 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (voucher.ExpiresUtc.HasValue && voucher.ExpiresUtc.Value <= now)
        {
            return false;
        }

        if (voucher.MaxRedemptions.HasValue && voucher.MaxRedemptions.Value > 0 && voucher.UsedCount >= voucher.MaxRedemptions.Value)
        {
            return false;
        }

        return true;
    }

    private bool IsVoucherValidForCart(Voucher voucher, IReadOnlyCollection<CartLine> cartLines)
    {
        if (cartLines.Count == 0)
        {
            return false;
        }

        if (!IsVoucherCurrentlyValid(voucher))
        {
            return false;
        }

        if (voucher.AppliesToCourseId.HasValue && !cartLines.Any(line => line.CourseId == voucher.AppliesToCourseId.Value))
        {
            return false;
        }

        return true;
    }

    private static List<CartLine> BuildCartLines(IEnumerable<CartItemView> items) =>
        items.Select(item => new CartLine(item.Course.Id, item.Course, item.Quantity)).ToList();

    private static List<CartLine> BuildCartLines(IEnumerable<CartItem> cart, CoursePricingContext context)
    {
        var lines = new List<CartLine>();
        foreach (var item in cart)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }

            if (!context.CourseMap.TryGetValue(item.CourseId, out var course))
            {
                continue;
            }

            lines.Add(new CartLine(course.Id, course, item.Quantity));
        }

        return lines;
    }

    private async Task<decimal> CalculateTotalAsync(List<CartItem> cart, CoursePricingContext? pricingContext = null)
    {
        var context = pricingContext;
        if (context == null)
        {
            var ids = cart.Select(c => c.CourseId).ToList();
            context = await BuildCoursePricingContextAsync(ids);
        }

        var courseMap = context.CourseMap;
        decimal total = 0m;
        var removed = false;

        foreach (var ci in cart.ToList())
        {
            if (courseMap.TryGetValue(ci.CourseId, out var course))
            {
                total += ci.Quantity * course.Price;
            }
            else
            {
                cart.Remove(ci);
                removed = true;
            }
        }

        if (removed)
        {
            _cartService.SetItems(HttpContext.Session, cart);
            ErrorMessage = "Some items were removed from your cart because they are no longer available.";
        }

        var bundleIds = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        if (bundleIds.Count > 0)
        {
            var blocks = await _context.CourseBlocks
                .AsNoTracking()
                .Include(b => b.Modules)
                .Where(b => bundleIds.Contains(b.Id))
                .ToListAsync();

            foreach (var block in blocks.ToList())
            {
                var moduleIds = block.Modules.Select(m => m.Id).ToList();
                if (moduleIds.All(id => cart.Any(ci => ci.CourseId == id)))
                {
                    decimal moduleSum = 0m;
                    foreach (var moduleId in moduleIds)
                    {
                        if (courseMap.TryGetValue(moduleId, out var moduleCourse))
                        {
                            moduleSum += moduleCourse.Price;
                        }
                        else
                        {
                            var module = block.Modules.FirstOrDefault(m => m.Id == moduleId);
                            if (module != null)
                            {
                                moduleSum += module.Price;
                            }
                        }
                    }

                    total -= moduleSum;
                    total += block.Price;
                }
                else
                {
                    bundleIds.Remove(block.Id);
                }
            }
        }

        HttpContext.Session.SetObject("Bundles", bundleIds);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private PricingBreakdown BuildOrderPricing(List<CartItem> cart, CoursePricingContext context, decimal total)
    {
        var breakdown = new PricingBreakdown();
        if (!cart.Any())
        {
            return breakdown;
        }

        var courseMap = context.CourseMap;
        var lines = new List<(CartItem CartItem, Course Course, decimal BaseTotal)>();

        foreach (var cartItem in cart)
        {
            if (cartItem.Quantity <= 0)
            {
                continue;
            }

            if (!courseMap.TryGetValue(cartItem.CourseId, out var course))
            {
                continue;
            }

            var baseTotal = course.Price * cartItem.Quantity;
            lines.Add((cartItem, course, baseTotal));
        }

        if (!lines.Any())
        {
            return breakdown;
        }

        var finalTotal = Math.Round(Math.Max(total, 0m), 2, MidpointRounding.AwayFromZero);
        var baseSum = lines.Sum(l => l.BaseTotal);
        var factor = baseSum > 0m ? finalTotal / baseSum : 0m;
        decimal allocatedTotal = 0m;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            decimal lineTotal;

            if (baseSum == 0m && finalTotal > 0m)
            {
                lineTotal = i == lines.Count - 1
                    ? Math.Round(finalTotal - allocatedTotal, 2, MidpointRounding.AwayFromZero)
                    : Math.Round(finalTotal / lines.Count, 2, MidpointRounding.AwayFromZero);
            }
            else if (i == lines.Count - 1)
            {
                lineTotal = Math.Round(finalTotal - allocatedTotal, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                lineTotal = Math.Round(line.BaseTotal * factor, 2, MidpointRounding.AwayFromZero);
            }

            allocatedTotal += lineTotal;

            var quantity = line.CartItem.Quantity;
            decimal unitPriceExcl = 0m;
            decimal lineVat = 0m;

            if (quantity > 0 && lineTotal > 0m)
            {
                var linePriceExcl = Math.Round(lineTotal / (1 + VatRate), 2, MidpointRounding.AwayFromZero);
                unitPriceExcl = Math.Round(linePriceExcl / quantity, 2, MidpointRounding.AwayFromZero);
                lineVat = lineTotal - linePriceExcl;
            }

            breakdown.Items.Add(new OrderItem
            {
                CourseId = line.Course.Id,
                Quantity = quantity,
                UnitPriceExclVat = unitPriceExcl,
                Vat = lineVat,
                Total = lineTotal
            });
        }

        breakdown.Vat = Math.Round(breakdown.Items.Sum(i => i.Vat), 2, MidpointRounding.AwayFromZero);
        breakdown.PriceExclVat = Math.Round(breakdown.Items.Sum(i => i.Total - i.Vat), 2, MidpointRounding.AwayFromZero);

        return breakdown;
    }

    private async Task<CoursePricingContext> BuildCoursePricingContextAsync(IReadOnlyCollection<int> courseIds)
    {
        var idList = courseIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new CoursePricingContext(new List<Course>(), new Dictionary<int, decimal>());
        }

        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => idList.Contains(c.Id))
            .ToListAsync();

        var priceMap = await GetEffectivePriceMapAsync(courses);
        foreach (var course in courses)
        {
            if (priceMap.TryGetValue(course.Id, out var price))
            {
                course.Price = price;
            }
        }

        return new CoursePricingContext(courses, priceMap);
    }

    private async Task<Dictionary<int, decimal>> GetEffectivePriceMapAsync(IReadOnlyCollection<Course> courses)
    {
        var result = new Dictionary<int, decimal>();
        if (courses.Count == 0)
        {
            return result;
        }

        var courseList = courses.ToList();
        var courseIds = courseList.Select(c => c.Id).ToList();

        var termStarts = await _context.CourseTerms
            .AsNoTracking()
            .Where(term => courseIds.Contains(term.CourseId) && term.IsActive)
            .Select(term => new { term.CourseId, term.StartUtc })
            .ToListAsync();

        var earliestTermMap = termStarts
            .GroupBy(term => term.CourseId)
            .ToDictionary(group => group.Key, group => group.Min(t => t.StartUtc));

        var schedules = await _context.Set<PriceSchedule>()
            .AsNoTracking()
            .Where(schedule => courseIds.Contains(schedule.CourseId))
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;

        foreach (var course in courseList)
        {
            var referenceTime = GetReferenceTimeForCourse(course, earliestTermMap, nowUtc);
            var schedule = schedules
                .Where(s => s.CourseId == course.Id && s.FromUtc <= referenceTime && s.ToUtc >= referenceTime)
                .OrderByDescending(s => s.FromUtc)
                .FirstOrDefault();

            decimal price;
            if (schedule != null)
            {
                price = Math.Round(schedule.NewPriceExcl * (1 + VatRate), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                price = course.Price;
            }

            result[course.Id] = price;
        }

        return result;
    }

    private static DateTime GetReferenceTimeForCourse(
        Course course,
        IReadOnlyDictionary<int, DateTime> earliestTermMap,
        DateTime defaultValue)
    {
        if (earliestTermMap.TryGetValue(course.Id, out var startUtc))
        {
            return startUtc;
        }

        var courseDate = course.Date;
        if (courseDate != default)
        {
            if (courseDate.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(courseDate, DateTimeKind.Utc);
            }

            return courseDate.ToUniversalTime();
        }

        return defaultValue;
    }

    private sealed class CoursePricingContext
    {
        public CoursePricingContext(List<Course> courses, Dictionary<int, decimal> priceMap)
        {
            Courses = courses;
            PriceMap = priceMap;
            CourseMap = courses.ToDictionary(c => c.Id);
        }

        public List<Course> Courses { get; }
        public Dictionary<int, Course> CourseMap { get; }
        public Dictionary<int, decimal> PriceMap { get; }
    }

    private sealed class PricingBreakdown
    {
        public List<OrderItem> Items { get; } = new();
        public decimal PriceExclVat { get; set; }
        public decimal Vat { get; set; }
    }

    private sealed record CartLine(int CourseId, Course Course, int Quantity)
    {
        public decimal LineTotal => Course.Price * Quantity;
    }

    public class CartItemView
    {
        public Course Course { get; set; } = default!;
        public int Quantity { get; set; }
    }
}
