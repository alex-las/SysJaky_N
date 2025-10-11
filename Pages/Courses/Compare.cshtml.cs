using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class CompareModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<CompareModel> _localizer;

    public IStringLocalizer<CompareModel> Localizer => _localizer;

    public CompareModel(ApplicationDbContext context, IStringLocalizer<CompareModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public IList<Course> Courses { get; private set; } = new List<Course>();

    public async Task<IActionResult> OnGetAsync(string? ids)
    {
        var requestedIds = ParseCourseIds(ids);
        if (requestedIds.Count == 0)
        {
            Courses = new List<Course>();
            return Page();
        }

        var idSet = new HashSet<int>(requestedIds);
        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => idSet.Contains(c.Id))
            .ToListAsync();

        var courseById = courses.ToDictionary(c => c.Id);
        Courses = requestedIds
            .Where(courseById.ContainsKey)
            .Select(id => courseById[id])
            .ToList();

        return Page();
    }

    private static List<int> ParseCourseIds(string? ids)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(ids))
        {
            return result;
        }

        var seen = new HashSet<int>();
        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (result.Count >= 3)
            {
                break;
            }

            if (int.TryParse(part, out var value) && seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
