using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

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

        public IList<Course> PicksForPersona { get; set; } = new List<Course>();
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

            PicksForPersona = recommended
                .Select(x => x.Course)
                .ToList();

            FreshNews = await _context.Articles
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync();
        }
    }
}
