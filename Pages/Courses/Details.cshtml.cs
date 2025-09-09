using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Course Course { get; set; } = null!;
    public IList<CourseReview> Reviews { get; set; } = new List<CourseReview>();

    [BindProperty]
    public CourseReview NewReview { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        Reviews = await _context.CourseReviews
            .Where(r => r.CourseId == id)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        // TODO: Add selected course to cart
        return RedirectToPage("/Cart/Index");
    }

    [Authorize]
    public async Task<IActionResult> OnPostReviewAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        if (!ModelState.IsValid)
        {
            Reviews = await _context.CourseReviews
                .Where(r => r.CourseId == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return Page();
        }

        NewReview.CourseId = id;
        NewReview.UserId = _userManager.GetUserId(User)!;
        NewReview.CreatedAt = DateTime.UtcNow;
        _context.CourseReviews.Add(NewReview);
        await _context.SaveChangesAsync();

        return RedirectToPage(new { id });
    }
}

