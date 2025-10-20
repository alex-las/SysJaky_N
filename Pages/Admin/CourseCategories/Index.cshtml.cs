using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using System.Collections.Generic;
using System.Linq;

namespace SysJaky_N.Pages.Admin.CourseCategories;

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

    public IList<CourseCategoryListItem> Categories { get; private set; } = new List<CourseCategoryListItem>();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        Categories = await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CourseCategoryListItem(
                category.Id,
                category.Name,
                category.Slug,
                category.SortOrder,
                category.IsActive,
                category.Courses.Count(course => course.IsActive)))
            .ToListAsync();
    }

    public record CourseCategoryListItem(int Id, string Name, string Slug, int SortOrder, bool IsActive, int CourseCount);
}
