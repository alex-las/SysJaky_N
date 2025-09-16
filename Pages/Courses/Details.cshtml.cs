using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Services;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CartService _cartService;

    public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, CartService cartService)
    {
        _context = context;
        _userManager = userManager;
        _cartService = cartService;
    }

    public Course Course { get; set; } = null!;
    public IList<CourseReview> Reviews { get; set; } = new List<CourseReview>();
    public CourseBlock? CourseBlock { get; set; }
    public IList<Lesson> Lessons { get; set; } = new List<Lesson>();
    public Dictionary<int, LessonProgress> ProgressByLessonId { get; set; } = new();

    [BindProperty]
    public CourseReview NewReview { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        await LoadPageDataAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var result = await _cartService.AddToCartAsync(HttpContext.Session, id);
        if (!result.Success)
        {
            TempData["CartError"] = result.ErrorMessage;
            return RedirectToPage(new { id });
        }

        return RedirectToPage("/Cart");
    }

    public async Task<IActionResult> OnPostOrderBlockAsync(int blockId)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(b => b.Id == blockId);
        if (block == null) return NotFound();
        var snapshot = _cartService.GetItems(HttpContext.Session);
        foreach (var module in block.Modules)
        {
            var addResult = await _cartService.AddToCartAsync(HttpContext.Session, module.Id);
            if (!addResult.Success)
            {
                _cartService.SetItems(HttpContext.Session, snapshot);
                TempData["CartError"] = addResult.ErrorMessage;
                return RedirectToPage("/Cart");
            }
        }
        var bundles = HttpContext.Session.GetObject<List<int>>("Bundles") ?? new List<int>();
        if (!bundles.Contains(block.Id))
        {
            bundles.Add(block.Id);
            HttpContext.Session.SetObject("Bundles", bundles);
        }
        return RedirectToPage("/Cart");
    }

    [Authorize]
    public async Task<IActionResult> OnPostAddToWishlistAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }
        bool exists = await _context.WishlistItems.AnyAsync(w => w.UserId == userId && w.CourseId == id);
        if (!exists)
        {
            _context.WishlistItems.Add(new WishlistItem { UserId = userId, CourseId = id });
            await _context.SaveChangesAsync();
        }
        return RedirectToPage(new { id });
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
            await LoadPageDataAsync(id);
            return Page();
        }

        NewReview.CourseId = id;
        NewReview.UserId = _userManager.GetUserId(User)!;
        NewReview.CreatedAt = DateTime.UtcNow;
        _context.CourseReviews.Add(NewReview);
        await _context.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    [Authorize]
    public async Task<IActionResult> OnPostUpdateProgressAsync(int id, int lessonId, int progress)
    {
        progress = Math.Clamp(progress, 0, 100);

        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }

        var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.CourseId == id);
        if (lesson == null)
        {
            return NotFound();
        }

        var existing = await _context.LessonProgresses.FindAsync(lessonId, userId);
        if (existing == null)
        {
            existing = new LessonProgress
            {
                LessonId = lessonId,
                UserId = userId,
                Progress = progress,
                LastSeenUtc = DateTime.UtcNow
            };
            _context.LessonProgresses.Add(existing);
        }
        else
        {
            existing.Progress = progress;
            existing.LastSeenUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    private async Task LoadPageDataAsync(int courseId)
    {
        if (Course.CourseBlockId.HasValue)
        {
            CourseBlock = await _context.CourseBlocks
                .Include(b => b.Modules)
                .FirstOrDefaultAsync(b => b.Id == Course.CourseBlockId.Value);
        }
        else
        {
            CourseBlock = null;
        }

        Reviews = await _context.CourseReviews
            .Where(r => r.CourseId == courseId && r.IsPublic)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        Lessons = await _context.Lessons
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.Order)
            .ThenBy(l => l.Id)
            .ToListAsync();

        ProgressByLessonId = new Dictionary<int, LessonProgress>();

        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(userId) && Lessons.Any())
        {
            var lessonIds = Lessons.Select(l => l.Id).ToList();
            var progressEntries = await _context.LessonProgresses
                .Where(lp => lp.UserId == userId && lessonIds.Contains(lp.LessonId))
                .ToListAsync();

            ProgressByLessonId = progressEntries.ToDictionary(lp => lp.LessonId);
        }
    }
}

