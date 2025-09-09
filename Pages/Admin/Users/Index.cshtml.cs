using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IList<UserViewModel> Users { get; set; } = new List<UserViewModel>();

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public async Task OnGetAsync()
    {
        var users = await _context.Users.ToListAsync();
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
    }
}
