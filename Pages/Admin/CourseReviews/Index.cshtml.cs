using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseReviews;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<CourseReview> Reviews { get; set; } = new List<CourseReview>();

    public async Task OnGetAsync()
    {
        Reviews = await _context.CourseReviews
            .Include(r => r.Course)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var review = await _context.CourseReviews.FindAsync(id);
        if (review != null)
        {
            _context.CourseReviews.Remove(review);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
