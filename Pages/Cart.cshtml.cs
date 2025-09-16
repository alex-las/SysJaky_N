using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages;

[Authorize]
public class CartModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;

    public CartModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _emailSender = emailSender;
        _auditService = auditService;
    }

    public List<CartItemView> Items { get; set; } = new();
    public decimal Total { get; set; }
    public Voucher? AppliedVoucher { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CourseBlock> BundleOffers { get; set; } = new();
    public List<CourseBlock> AppliedBundles { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadCartAsync();
        await ApplyStoredVoucherAsync();
    }

    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        if (!cart.Any()) return RedirectToPage();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var userId = user.Id;

        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();
        var total = await CalculateTotalAsync(cart);
        var cartLines = BuildCartLines(cart, courses);
        Voucher? voucher = null;
        var voucherId = HttpContext.Session.GetInt32("VoucherId");
        if (voucherId.HasValue)
        {
            voucher = await _context.Vouchers.FindAsync(voucherId.Value);
            if (voucher == null || !IsVoucherValidForCart(voucher, cartLines))
            {
                voucher = null;
            }
        }
        if (voucher != null)
        {
            var discount = CalculateVoucherDiscount(total, voucher, cartLines);
            total -= discount;
        }
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Select(c => new OrderItem { CourseId = c.CourseId, Quantity = c.Quantity }).ToList(),
            TotalPrice = total,
            VoucherId = voucher?.Id
        };
        _context.Orders.Add(order);
        if (voucher != null)
        {
            voucher.UsedCount += 1;
        }
        await _context.SaveChangesAsync();
        await _auditService.LogAsync(userId, "OrderCreated", $"Order {order.Id} created");
        await _emailSender.SendEmailAsync(user.Email!, "Order Created", $"Your order {order.Id} has been created.");
        HttpContext.Session.Remove("Cart");
        HttpContext.Session.Remove("VoucherId");
        HttpContext.Session.Remove("Bundles");
        return RedirectToPage("/Orders/Index");
    }

    public async Task<IActionResult> OnPostApplyVoucherAsync(string code)
    {
        await LoadCartAsync();
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
        var cartLines = BuildCartLines(Items);
        if (voucher == null || !IsVoucherValidForCart(voucher, cartLines))
        {
            ErrorMessage = "Invalid voucher code";
            HttpContext.Session.Remove("VoucherId");
            return Page();
        }
        ErrorMessage = null;
        HttpContext.Session.SetInt32("VoucherId", voucher.Id);
        ApplyVoucher(voucher, cartLines);
        return Page();
    }

    private async Task LoadCartAsync()
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();
        var cartLines = BuildCartLines(cart, courses);
        Items = cartLines.Select(line => new CartItemView { Course = line.Course, Quantity = line.Quantity }).ToList();

        var bundleIds = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        var blocks = await _context.CourseBlocks.Include(b => b.Modules).ToListAsync();
        foreach (var block in blocks)
        {
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

        Total = await CalculateTotalAsync(cart);
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
        Total -= DiscountAmount;
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

        decimal discount = voucher.Type switch
        {
            VoucherType.Percentage => Math.Round(targetTotal * ClampPercentage(voucher.Value) / 100m, 2, MidpointRounding.AwayFromZero),
            VoucherType.FixedAmount => Math.Min(voucher.Value, targetTotal),
            _ => 0m
        };

        return Math.Min(discount, total);
    }

    private static decimal ClampPercentage(decimal value)
    {
        if (value < 0)
        {
            return 0;
        }
        if (value > 100)
        {
            return 100;
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

    private static List<CartLine> BuildCartLines(IEnumerable<CartItemView> items)
    {
        return items
            .Select(item => new CartLine(item.Course.Id, item.Course, item.Quantity))
            .ToList();
    }

    private static List<CartLine> BuildCartLines(IEnumerable<CartItem> cart, IEnumerable<Course> courses)
    {
        return cart
            .Join(courses, ci => ci.CourseId, course => course.Id, (ci, course) => new CartLine(course.Id, course, ci.Quantity))
            .ToList();
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

    private async Task<decimal> CalculateTotalAsync(List<CartItem> cart)
    {
        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();

        decimal total = 0m;
        var removed = false;
        foreach (var ci in cart.ToList())
        {
            var course = courses.FirstOrDefault(c => c.Id == ci.CourseId);
            if (course != null)
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
            HttpContext.Session.SetObject("Cart", cart);
            ErrorMessage = "Some items were removed from your cart because they are no longer available.";
        }

        var bundleIds = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        var blocks = await _context.CourseBlocks.Include(b => b.Modules).Where(b => bundleIds.Contains(b.Id)).ToListAsync();
        foreach (var block in blocks.ToList())
        {
            var moduleIds = block.Modules.Select(m => m.Id).ToList();
            if (moduleIds.All(id => cart.Any(ci => ci.CourseId == id)))
            {
                var moduleSum = block.Modules.Sum(m => m.Price);
                total -= moduleSum;
                total += block.Price;
            }
            else
            {
                bundleIds.Remove(block.Id);
            }
        }
        HttpContext.Session.SetObject("Bundles", bundleIds);
        return total;
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
