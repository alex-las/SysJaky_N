using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;

namespace SysJaky_N.Pages;

[Authorize]
public class CartModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<CartItemView> Items { get; set; } = new();
    public decimal Total { get; set; }

    public async Task OnGetAsync()
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();
        Items = cart.Join(courses, c => c.CourseId, c2 => c2.Id, (c, c2) => new CartItemView { Course = c2, Quantity = c.Quantity }).ToList();
        Total = Items.Sum(i => i.Course.Price * i.Quantity);
    }

    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        if (!cart.Any()) return RedirectToPage();

        var userId = _userManager.GetUserId(User);
        if (userId == null) return Challenge();

        var ids = cart.Select(c => c.CourseId).ToList();
        var courses = await _context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync();
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Select(c => new OrderItem { CourseId = c.CourseId, Quantity = c.Quantity }).ToList(),
            TotalPrice = cart.Sum(ci => ci.Quantity * courses.First(c => c.Id == ci.CourseId).Price)
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        HttpContext.Session.Remove("Cart");
        return RedirectToPage("/Orders/Index");
    }

    public class CartItemView
    {
        public Course Course { get; set; } = default!;
        public int Quantity { get; set; }
    }
}
