using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Users;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DeleteModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public ApplicationUser UserEntity { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        UserEntity = user;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        await _userManager.DeleteAsync(user);
        return RedirectToPage("Index");
    }
}
