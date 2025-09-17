using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public CourseTerm Term { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var term = await LoadTermAsync(id);
        if (term == null)
        {
            return NotFound();
        }

        Term = term;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var term = await _context.CourseTerms
            .Include(t => t.Course)
            .Include(t => t.Instructor)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term == null)
        {
            return NotFound();
        }

        if (term.SeatsTaken > 0)
        {
            ErrorMessage = "The term cannot be deleted while seats are already taken.";
            Term = term;
            return Page();
        }

        _context.CourseTerms.Remove(term);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private Task<CourseTerm?> LoadTermAsync(int id)
    {
        return _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Instructor)
            .FirstOrDefaultAsync(t => t.Id == id);
    }
}
