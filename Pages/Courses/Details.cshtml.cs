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
    private readonly ICacheService _cacheService;

    public DetailsModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CartService cartService,
        ICacheService cacheService)
    {
        _context = context;
        _userManager = userManager;
        _cartService = cartService;
        _cacheService = cacheService;
    }

    public Course Course { get; set; } = null!;
    public IList<CourseReview> Reviews { get; set; } = new List<CourseReview>();
    public CourseBlock? CourseBlock { get; set; }
    public IList<Lesson> Lessons { get; set; } = new List<Lesson>();
    public Dictionary<int, LessonProgress> ProgressByLessonId { get; set; } = new();
    public IList<CourseTermSummary> Terms { get; set; } = new List<CourseTermSummary>();

    [BindProperty]
    public CourseReview NewReview { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadCourseDataAsync(id))
        {
            return NotFound();
        }

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
            if (!await LoadCourseDataAsync(id))
            {
                return NotFound();
            }

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

    private async Task<bool> LoadCourseDataAsync(int courseId)
    {
        var cacheEntry = await _cacheService.GetCourseDetailAsync(courseId, async () =>
        {
            var course = await _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseGroup)
                .Include(c => c.CourseTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                return null;
            }

            CourseBlock? courseBlock = null;
            if (course.CourseBlockId.HasValue)
            {
                courseBlock = await _context.CourseBlocks
                    .AsNoTracking()
                    .Include(b => b.Modules)
                    .FirstOrDefaultAsync(b => b.Id == course.CourseBlockId.Value);
            }

            var reviews = await _context.CourseReviews
                .AsNoTracking()
                .Where(r => r.CourseId == courseId && r.IsPublic)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.CourseId == courseId)
                .OrderBy(l => l.Order)
                .ThenBy(l => l.Id)
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;
            var terms = await _context.CourseTerms
                .AsNoTracking()
                .Where(term => term.CourseId == courseId && term.IsActive && term.EndUtc >= nowUtc)
                .OrderBy(term => term.StartUtc)
                .ToListAsync();

            return new CourseDetailCacheEntry(course, courseBlock, reviews, lessons, terms);
        });

        if (cacheEntry == null)
        {
            return false;
        }

        Course = cacheEntry.Course;
        CourseBlock = cacheEntry.CourseBlock;
        Reviews = cacheEntry.Reviews.ToList();
        Lessons = cacheEntry.Lessons.ToList();
        var isOnline = ResolveIsOnline(cacheEntry.Course);
        var location = ResolveLocation(cacheEntry.Course);
        Terms = cacheEntry.Terms
            .Select(term => new CourseTermSummary(
                term.Id,
                DateTime.SpecifyKind(term.StartUtc, DateTimeKind.Utc).ToLocalTime(),
                isOnline,
                location,
                Math.Max(0, term.Capacity - term.SeatsTaken)))
            .ToList();

        await LoadLessonProgressAsync();
        return true;
    }

    private async Task LoadLessonProgressAsync()
    {
        ProgressByLessonId = new Dictionary<int, LessonProgress>();

        if (Lessons.Count == 0)
        {
            return;
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var lessonIds = Lessons.Select(l => l.Id).ToList();
        var progressEntries = await _context.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == userId && lessonIds.Contains(lp.LessonId))
            .ToListAsync();

        ProgressByLessonId = progressEntries.ToDictionary(lp => lp.LessonId);
    }

    public record CourseTermSummary(
        int Id,
        DateTime StartsAt,
        bool IsOnline,
        string Location,
        int SeatsLeft);

    private static bool ResolveIsOnline(Course? course)
    {
        if (course == null)
        {
            return false;
        }

        return course.Type == CourseType.Online || course.Mode == CourseMode.SelfPaced;
    }

    private static string ResolveLocation(Course? course)
    {
        if (course == null)
        {
            return "Bude upřesněno";
        }

        return course.Type switch
        {
            CourseType.Online => "Online",
            CourseType.Hybrid => "Hybridní (kombinace)",
            CourseType.InPerson => "Prezenčně",
            _ => "Bude upřesněno"
        };
    }
}

