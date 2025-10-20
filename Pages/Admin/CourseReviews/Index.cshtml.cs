using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseReviews;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    private readonly IStringLocalizer<IndexModel> _localizer;

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<CourseReview> Reviews { get; set; } = new List<CourseReview>();

    [BindProperty(SupportsGet = true)]
    public string? CourseSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? RatingSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? IsPublicFilter { get; set; }

    [BindProperty]
    public List<int> SelectedReviewIds { get; set; } = new();

    public double? AverageRating { get; private set; }

    public int PendingReviewsCount { get; private set; }

    public int TotalReviewsCount { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var review = await _context.CourseReviews.FindAsync(id);
        if (review == null)
        {
            return NotFound(_localizer["CourseReviewNotFound"]);
        }

        _context.CourseReviews.Remove(review);
        await _context.SaveChangesAsync();
        StatusMessage = _localizer["CourseReviewDeleted"];
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostTogglePublishAsync(int id, bool publish)
    {
        var review = await _context.CourseReviews.FindAsync(id);
        if (review == null)
        {
            return NotFound(_localizer["CourseReviewNotFound"]);
        }

        if (publish && !review.IsPublic)
        {
            review.IsPublic = true;
            review.PublishedAtUtc = DateTime.UtcNow;
        }
        else if (!publish && review.IsPublic)
        {
            review.IsPublic = false;
            review.PublishedAtUtc = null;
        }

        await _context.SaveChangesAsync();
        StatusMessage = publish
            ? _localizer["CourseReviewPublished"]
            : _localizer["CourseReviewUnpublished"];

        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync()
    {
        if (SelectedReviewIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["NoReviewsSelected"]);
            await LoadPageDataAsync();
            return Page();
        }

        var reviewsToDelete = await _context.CourseReviews
            .Where(r => SelectedReviewIds.Contains(r.Id))
            .ToListAsync();

        if (reviewsToDelete.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["NoReviewsMatchedSelection"]);
            await LoadPageDataAsync();
            return Page();
        }

        _context.CourseReviews.RemoveRange(reviewsToDelete);
        await _context.SaveChangesAsync();

        StatusMessage = _localizer["CourseReviewsDeleted", reviewsToDelete.Count];
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostBulkSetPublicationAsync(bool publish)
    {
        if (SelectedReviewIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["NoReviewsSelected"]);
            await LoadPageDataAsync();
            return Page();
        }

        var reviewsToUpdate = await _context.CourseReviews
            .Where(r => SelectedReviewIds.Contains(r.Id))
            .ToListAsync();

        if (reviewsToUpdate.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["NoReviewsMatchedSelection"]);
            await LoadPageDataAsync();
            return Page();
        }

        var now = DateTime.UtcNow;
        foreach (var review in reviewsToUpdate)
        {
            if (publish)
            {
                if (!review.IsPublic)
                {
                    review.IsPublic = true;
                    review.PublishedAtUtc = now;
                }
            }
            else if (review.IsPublic)
            {
                review.IsPublic = false;
                review.PublishedAtUtc = null;
            }
        }

        await _context.SaveChangesAsync();

        StatusMessage = publish
            ? _localizer["CourseReviewsPublished", reviewsToUpdate.Count]
            : _localizer["CourseReviewsUnpublished", reviewsToUpdate.Count];

        return RedirectToCurrentPage();
    }

    private async Task LoadPageDataAsync()
    {
        SelectedReviewIds = SelectedReviewIds ?? new List<int>();

        var query = _context.CourseReviews.AsQueryable();

        if (!string.IsNullOrWhiteSpace(CourseSearch))
        {
            var term = CourseSearch.Trim();
            query = query.Where(r => r.Course != null && EF.Functions.Like(r.Course!.Title, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(UserSearch))
        {
            var term = UserSearch.Trim();
            query = query.Where(r => r.User != null &&
                ((r.User!.UserName != null && EF.Functions.Like(r.User.UserName, $"%{term}%")) ||
                 (r.User.Email != null && EF.Functions.Like(r.User.Email, $"%{term}%"))));
        }

        if (RatingSearch.HasValue)
        {
            query = query.Where(r => r.Rating == RatingSearch.Value);
        }

        if (IsPublicFilter.HasValue)
        {
            query = query.Where(r => r.IsPublic == IsPublicFilter.Value);
        }

        TotalReviewsCount = await query.CountAsync();

        AverageRating = TotalReviewsCount > 0
            ? await query.AverageAsync(r => (double)r.Rating)
            : null;

        PendingReviewsCount = await _context.CourseReviews.CountAsync(r => !r.IsPublic);

        Reviews = await query
            .Include(r => r.Course)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    private RedirectToPageResult RedirectToCurrentPage()
    {
        return RedirectToPage(new
        {
            CourseSearch,
            UserSearch,
            RatingSearch,
            IsPublicFilter
        });
    }
}
