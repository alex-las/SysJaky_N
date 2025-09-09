using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<Course> Courses { get; set; } = new List<Course>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        const int pageSize = 10;
        var query = _context.Courses.OrderBy(c => c.Date);
        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Courses = await query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public IActionResult OnPostAddToCart(int courseId)
    {
        var cart = HttpContext.Session.GetObject<List<CartItem>>("Cart") ?? new List<CartItem>();
        var item = cart.FirstOrDefault(c => c.CourseId == courseId);
        if (item == null)
        {
            cart.Add(new CartItem { CourseId = courseId, Quantity = 1 });
        }
        else
        {
            item.Quantity++;
        }
        HttpContext.Session.SetObject("Cart", cart);
        return RedirectToPage();
    }
}
