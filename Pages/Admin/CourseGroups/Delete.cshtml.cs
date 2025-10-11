using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseGroups;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DeleteModel> _localizer;

    public DeleteModel(ApplicationDbContext context, IStringLocalizer<DeleteModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public CourseGroup CourseGroup { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ViewData["Title"] = _localizer["Title"];
        var group = await _context.CourseGroups.FindAsync(id);
        if (group == null)
        {
            return NotFound();
        }
        CourseGroup = group;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ViewData["Title"] = _localizer["Title"];
        var group = await _context.CourseGroups.FindAsync(id);
        if (group != null)
        {
            _context.CourseGroups.Remove(group);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
