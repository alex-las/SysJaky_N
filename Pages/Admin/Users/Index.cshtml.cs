using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;
using System.Linq;
using System;

namespace SysJaky_N.Pages.Admin.Users;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public IList<UserViewModel> Users { get; set; } = new List<UserViewModel>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    public int TotalPages { get; set; }
    public const int PageSize = 10;

    public IList<string> AllRoles { get; set; } = new List<string>();

    [BindProperty]
    public List<string> SelectedUserIds { get; set; } = new();

    [BindProperty]
    public string RoleName { get; set; } = string.Empty;

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public async Task OnGetAsync()
    {
        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrEmpty(Search))
        {
            query = query.Where(u => (u.Email ?? "").Contains(Search) || (u.UserName ?? "").Contains(Search));
        }
        var count = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(count / (double)PageSize);
        var users = await query.OrderBy(u => u.Email).Skip((PageIndex - 1) * PageSize).Take(PageSize).ToListAsync();
        foreach (var user in users)
        {
            var vm = new UserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            };
            vm.Roles = await _userManager.GetRolesAsync(user);
            Users.Add(vm);
        }
        AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
    }

    public async Task<IActionResult> OnPostAssignRoleAsync()
    {
        if (SelectedUserIds.Any() && !string.IsNullOrEmpty(RoleName))
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var id in SelectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    continue;
                if (!await _userManager.IsInRoleAsync(user, RoleName))
                {
                    await _userManager.AddToRoleAsync(user, RoleName);
                    await _auditService.LogAsync(adminId, "RoleAssigned", $"User {user.Id} assigned role {RoleName}");
                }
            }
        }
        return RedirectToPage(new { Search, PageIndex });
    }

    public async Task<IActionResult> OnPostRemoveRoleAsync()
    {
        if (SelectedUserIds.Any() && !string.IsNullOrEmpty(RoleName))
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var id in SelectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    continue;
                if (await _userManager.IsInRoleAsync(user, RoleName))
                {
                    await _userManager.RemoveFromRoleAsync(user, RoleName);
                    await _auditService.LogAsync(adminId, "RoleRemoved", $"User {user.Id} removed from role {RoleName}");
                }
            }
        }
        return RedirectToPage(new { Search, PageIndex });
    }

    public async Task<IActionResult> OnPostBlockAsync()
    {
        if (SelectedUserIds.Any())
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var id in SelectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    continue;
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                await _auditService.LogAsync(adminId, "AccountDeactivated", $"User {user.Id} blocked");
            }
        }
        return RedirectToPage(new { Search, PageIndex });
    }

    public async Task<IActionResult> OnPostUnblockAsync()
    {
        if (SelectedUserIds.Any())
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            foreach (var id in SelectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    continue;
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _auditService.LogAsync(adminId, "AccountActivated", $"User {user.Id} unblocked");
            }
        }
        return RedirectToPage(new { Search, PageIndex });
    }
}
