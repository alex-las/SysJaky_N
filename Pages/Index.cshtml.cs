using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;
using System.Globalization;

namespace SysJaky_N.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<IndexModel> _localizer;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
        {
            _logger = logger;
            _context = context;
            _localizer = localizer;
        }

        public IList<CourseCardViewModel> PicksForPersona { get; set; } = new List<CourseCardViewModel>();
        public IList<Course> FastSoonest { get; set; } = new List<Course>();
        public IList<Article> FreshNews { get; set; } = new List<Article>();
        public IList<Testimonial> FeaturedTestimonials { get; set; } = new List<Testimonial>();
        public IReadOnlyList<HeroMetric> HeroMetrics { get; private set; } = System.Array.Empty<HeroMetric>();

        public async Task OnGetAsync(string? persona, string? goal)
        {
            ViewData["Title"] = _localizer["Title"];
            _logger.LogInformation("CurrentCulture={culture}, CurrentUICulture={ui}",
                CultureInfo.CurrentCulture.Name,
                CultureInfo.CurrentUICulture.Name);

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

            var snapshots = await _context.LoadTermSnapshotsAsync(recommendedCourses.Select(c => c.Id));
            var culture = CultureInfo.CurrentCulture;
            var addToCartUrl = Url.Page("/Courses/Index", pageHandler: "AddToCart") ?? "/Courses/Index?handler=AddToCart";

            PicksForPersona = recommendedCourses
                .Select(course =>
                {
                    var detailsUrl = Url.Page("/Courses/Details", new { id = course.Id }) ?? $"/Courses/Details/{course.Id}";
                    var wishlistUrl = Url.Page("/Courses/Details", new { id = course.Id, handler = "AddToWishlist" })
                        ?? $"/Courses/Details/{course.Id}?handler=AddToWishlist";

                    snapshots.TryGetValue(course.Id, out var snapshot);

                    return course.ToCourseCardViewModel(
                        culture,
                        detailsUrl,
                        addToCartUrl,
                        wishlistUrl,
                        snapshot);
                })
                .ToList();

            var now = DateTime.UtcNow;
            FreshNews = await _context.Articles
                .AsNoTracking()
                .Where(a => a.IsPublished && (a.PublishedAtUtc ?? a.CreatedAt) <= now)
                .OrderByDescending(a => a.PublishedAtUtc ?? a.CreatedAt)
                .Take(6)
                .ToListAsync();

            FeaturedTestimonials = await _context.Testimonials
                .AsNoTracking()
                .Where(t => t.IsPublished && t.ConsentGranted)
                .OrderByDescending(t => t.ConsentGrantedAtUtc ?? DateTime.MinValue)
                .ThenBy(t => t.FullName)
                .Take(8)
                .ToListAsync();
            HeroMetrics = new[]
            {
                new HeroMetric
                {
                    Value = "20+",
                    Label = _localizer["MetricExperienceLabel"].Value,
                    AriaLabel = _localizer["MetricExperienceAriaLabel", "20+"].Value,
                    RevealDelay = 0
                },
                new HeroMetric
                {
                    Value = "500+",
                    Label = _localizer["MetricClientsLabel"].Value,
                    AriaLabel = _localizer["MetricClientsAriaLabel", "500+"].Value,
                    RevealDelay = 50
                },
                new HeroMetric
                {
                    Value = "2000+",
                    Label = _localizer["MetricCoursesLabel"].Value,
                    AriaLabel = _localizer["MetricCoursesAriaLabel", "2000+"].Value,
                    RevealDelay = 100
                },
                new HeroMetric
                {
                    Value = "15+",
                    Label = _localizer["MetricInstructorsLabel"].Value,
                    AriaLabel = _localizer["MetricInstructorsAriaLabel", "15+"].Value,
                    RevealDelay = 150
                }
            };
        }

        public class HeroMetric
        {
            public string Value { get; init; } = string.Empty;
            public string Label { get; init; } = string.Empty;
            public string AriaLabel { get; init; } = string.Empty;
            public int RevealDelay { get; init; }
        }
    }
}
