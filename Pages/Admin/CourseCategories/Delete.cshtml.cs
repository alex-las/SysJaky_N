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
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IStringLocalizer<DeleteModel> _localizer;

    public DeleteModel(ApplicationDbContext context, ICacheService cacheService, IStringLocalizer<DeleteModel> localizer)
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

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ViewData["Title"] = _localizer["Title"];

        var category = await _context.CourseCategories.FindAsync(id);
        if (category != null)
        {
            _context.CourseCategories.Remove(category);
            await _context.SaveChangesAsync();
            _cacheService.InvalidateCourseList();
        }

        return RedirectToPage("Index");
    }
}
