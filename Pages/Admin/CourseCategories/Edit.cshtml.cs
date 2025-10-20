using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Threading.Tasks;

namespace SysJaky_N.Pages.Admin.CourseCategories;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IStringLocalizer<EditModel> _localizer;

    public EditModel(ApplicationDbContext context, ICacheService cacheService, IStringLocalizer<EditModel> localizer)
    {
        _context = context;
        _cacheService = cacheService;
        _localizer = localizer;
    }

    [BindProperty]
    public CourseCategory Category { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ViewData["Title"] = _localizer["Title"];

        var category = await _context.CourseCategories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        Category = category;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        var categoryToUpdate = await _context.CourseCategories.FirstOrDefaultAsync(c => c.Id == Category.Id);
        if (categoryToUpdate == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        categoryToUpdate.Name = Category.Name?.Trim() ?? string.Empty;
        categoryToUpdate.Slug = string.IsNullOrWhiteSpace(Category.Slug)
            ? string.Empty
            : Category.Slug.Trim().ToLowerInvariant();
        categoryToUpdate.Description = string.IsNullOrWhiteSpace(Category.Description)
            ? null
            : Category.Description.Trim();

        await _context.SaveChangesAsync();
        _cacheService.InvalidateCourseList();

        return RedirectToPage("Index");
    }
}
