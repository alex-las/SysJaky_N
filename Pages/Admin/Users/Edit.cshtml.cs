using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;
using System.Linq;

namespace SysJaky_N.Pages.Admin.Users;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public EditModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsLocked { get; set; }
        public List<RoleSelection> Roles { get; set; } = new();
    }

    public class RoleSelection
    {
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
        };

        var allRoles = await _roleManager.Roles.ToListAsync();
        foreach (var role in allRoles)
        {
            Input.Roles.Add(new RoleSelection
            {
                Name = role.Name!,
                Selected = await _userManager.IsInRoleAsync(user, role.Name!)
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var wasLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        user.Email = Input.Email;
        user.UserName = Input.Email;
        user.PhoneNumber = Input.PhoneNumber;
        await _userManager.SetLockoutEndDateAsync(user, Input.IsLocked ? DateTimeOffset.MaxValue : null);
        await _userManager.UpdateAsync(user);

        var selectedRoles = Input.Roles.Where(r => r.Selected).Select(r => r.Name).ToList();
        await _userManager.AddToRolesAsync(user, selectedRoles.Except(userRoles));
        await _userManager.RemoveFromRolesAsync(user, userRoles.Except(selectedRoles));

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        foreach (var role in selectedRoles.Except(userRoles))
        {
            await _auditService.LogAsync(adminId, "RoleAssigned", $"User {user.Id} assigned role {role}");
        }
        foreach (var role in userRoles.Except(selectedRoles))
        {
            await _auditService.LogAsync(adminId, "RoleRemoved", $"User {user.Id} removed from role {role}");
        }

        var isLocked = Input.IsLocked;
        if (isLocked != wasLocked)
        {
            await _auditService.LogAsync(adminId, isLocked ? "AccountDeactivated" : "AccountActivated", $"User {user.Id} {(isLocked ? "locked" : "unlocked")}");
        }

        return RedirectToPage("Index");
    }
}
