using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Models.ViewModels;
using System.Globalization;
using System.Security.Claims;

namespace SysJaky_N.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IList<CourseCardViewModel> PicksForPersonaCards { get; private set; } = new List<CourseCardViewModel>();
        public IList<Course> FastSoonest { get; set; } = new List<Course>();
        public IList<Article> FreshNews { get; set; } = new List<Article>();

        public async Task OnGetAsync(string? persona, string? goal)
        {
            var today = DateTime.Today;

            FastSoonest = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive && c.Date >= today)
                .OrderBy(c => c.Date)
                .ThenBy(c => c.Title)
                .Take(6)
                .ToListAsync();

            IQueryable<Course> baseQuery = _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseGroup)
                .Include(c => c.CourseTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(persona))
            {
                var personaPattern = $"%{persona.Trim()}%";
                baseQuery = baseQuery.Where(c =>
                    (c.CourseGroup != null && EF.Functions.Like(c.CourseGroup.Name, personaPattern)) ||
                    c.CourseTags.Any(ct => EF.Functions.Like(ct.Tag.Name, personaPattern)));
            }

            if (!string.IsNullOrWhiteSpace(goal))
            {
                var goalPattern = $"%{goal.Trim()}%";
                baseQuery = baseQuery.Where(c =>
                    (!string.IsNullOrEmpty(c.Description) && EF.Functions.Like(c.Description, goalPattern)) ||
                    c.CourseTags.Any(ct => EF.Functions.Like(ct.Tag.Name, goalPattern)));
            }

            var recommended = await baseQuery
                .GroupJoin(
                    _context.CourseReviews.AsNoTracking().Where(r => r.IsPublic),
                    course => course.Id,
                    review => review.CourseId,
                    (course, reviews) => new
                    {
                        Course = course,
                        AverageRating = reviews.Select(r => (double?)r.Rating).Average() ?? 0d,
                        ReviewCount = reviews.Count()
                    })
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.ReviewCount)
                .ThenBy(x => x.Course.Date)
                .Take(6)
                .ToListAsync();

            var recommendedCourses = recommended
                .Select(x => x.Course)
                .ToList();

            var upcomingTerms = await LoadUpcomingTermsAsync(recommendedCourses.Select(c => c.Id));
            var wishlisted = await LoadWishlistedCourseIdsAsync(recommendedCourses.Select(c => c.Id));
            var culture = CultureInfo.CurrentCulture;

            PicksForPersonaCards = recommendedCourses
                .Select(course =>
                {
                    upcomingTerms.TryGetValue(course.Id, out var term);
                    return CourseCardViewModelFactory.Create(course, term, wishlisted, Url, culture);
                })
                .ToList();

            FreshNews = await _context.Articles
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync();
        }

        private async Task<Dictionary<int, CourseTerm?>> LoadUpcomingTermsAsync(IEnumerable<int> courseIds)
        {
            var ids = courseIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, CourseTerm?>();
            }

            var nowUtc = DateTime.UtcNow;
            var terms = await _context.CourseTerms
                .AsNoTracking()
                .Where(t => ids.Contains(t.CourseId) && t.IsActive && t.StartUtc >= nowUtc)
                .OrderBy(t => t.StartUtc)
                .ToListAsync();

            var result = new Dictionary<int, CourseTerm?>();
            foreach (var term in terms)
            {
                if (!result.ContainsKey(term.CourseId))
                {
                    result[term.CourseId] = term;
                }
            }

            return result;
        }

        private async Task<HashSet<int>> LoadWishlistedCourseIdsAsync(IEnumerable<int> courseIds)
        {
            var ids = courseIds.Distinct().ToList();
            if (ids.Count == 0 || User.Identity?.IsAuthenticated != true)
            {
                return new HashSet<int>();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return new HashSet<int>();
            }

            var wishlisted = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId && ids.Contains(w.CourseId))
                .Select(w => w.CourseId)
                .ToListAsync();

            return wishlisted.ToHashSet();
        }
    }
}
