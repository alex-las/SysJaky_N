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
    public DiscountCode? AppliedDiscount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadCartAsync();
        await ApplyStoredDiscountAsync();
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
        var total = cart.Sum(ci => ci.Quantity * courses.First(c => c.Id == ci.CourseId).Price);
        DiscountCode? discount = null;
        var discountId = HttpContext.Session.GetInt32("DiscountCodeId");
        if (discountId.HasValue)
        {
            discount = await _context.DiscountCodes.FindAsync(discountId.Value);
            if (discount != null && discount.ExpiresAt > DateTime.UtcNow)
            {
                total -= CalculateDiscount(total, discount);
            }
            else
            {
                discount = null;
            }
        }
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Select(c => new OrderItem { CourseId = c.CourseId, Quantity = c.Quantity }).ToList(),
            TotalPrice = total,
            DiscountCodeId = discount?.Id
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync(userId, "OrderCreated", $"Order {order.Id} created");
        await _emailSender.SendEmailAsync(user.Email!, "Order Created", $"Your order {order.Id} has been created.");
        HttpContext.Session.Remove("Cart");
        HttpContext.Session.Remove("DiscountCodeId");
        return RedirectToPage("/Orders/Index");
    }

    public async Task<IActionResult> OnPostApplyDiscountAsync(string code)
    {
        await LoadCartAsync();
        var discount = await _context.DiscountCodes.FirstOrDefaultAsync(c => c.Code == code);
        if (discount == null || discount.ExpiresAt < DateTime.UtcNow)
        {
            ErrorMessage = "Invalid discount code";
            return Page();
        }
        HttpContext.Session.SetInt32("DiscountCodeId", discount.Id);
        ApplyDiscount(discount);
        return Page();
    }

    private async Task LoadCartAsync()
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();
        Items = cart.Join(courses, c => c.CourseId, c2 => c2.Id, (c, c2) => new CartItemView { Course = c2, Quantity = c.Quantity }).ToList();
        Total = Items.Sum(i => i.Course.Price * i.Quantity);
    }

    private async Task ApplyStoredDiscountAsync()
    {
        var id = HttpContext.Session.GetInt32("DiscountCodeId");
        if (id.HasValue)
        {
            var discount = await _context.DiscountCodes.FindAsync(id.Value);
            if (discount != null && discount.ExpiresAt > DateTime.UtcNow)
            {
                ApplyDiscount(discount);
            }
            else
            {
                HttpContext.Session.Remove("DiscountCodeId");
            }
        }
    }

    private void ApplyDiscount(DiscountCode discount)
    {
        AppliedDiscount = discount;
        DiscountAmount = CalculateDiscount(Total, discount);
        Total -= DiscountAmount;
    }

    private static decimal CalculateDiscount(decimal total, DiscountCode discount)
    {
        if (discount.Percentage.HasValue)
        {
            return Math.Round(total * discount.Percentage.Value / 100m, 2);
        }
        if (discount.Amount.HasValue)
        {
            return Math.Min(discount.Amount.Value, total);
        }
        return 0m;
    }

    public class CartItemView
    {
        public Course Course { get; set; } = default!;
        public int Quantity { get; set; }
    }
}
