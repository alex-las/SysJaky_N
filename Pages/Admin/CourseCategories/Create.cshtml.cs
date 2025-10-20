using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Threading.Tasks;

namespace SysJaky_N.Pages.Admin.CourseCategories;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public CreateModel(ApplicationDbContext context, ICacheService cacheService, IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _cacheService = cacheService;
        _localizer = localizer;
    }

    [BindProperty]
    public CourseCategory Category { get; set; } = new();

    public void OnGet()
    {
        ViewData["Title"] = _localizer["Title"];
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Category.Name = Category.Name?.Trim() ?? string.Empty;
        Category.Slug = string.IsNullOrWhiteSpace(Category.Slug)
            ? string.Empty
            : Category.Slug.Trim().ToLowerInvariant();
        Category.Description = string.IsNullOrWhiteSpace(Category.Description)
            ? null
            : Category.Description.Trim();
        Category.SortOrder = Category.SortOrder < 0 ? 0 : Category.SortOrder;

        _context.CourseCategories.Add(Category);
        await _context.SaveChangesAsync();
        _cacheService.InvalidateCourseList();

        return RedirectToPage("Index");
    }
}
