using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using InstructorModel = SysJaky_N.Models.Instructor;

namespace SysJaky_N.Pages.Admin.Instructors;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InstructorModel Instructor { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var instructor = await LoadInstructorAsync(id);
        if (instructor == null)
        {
            return NotFound();
        }

        Instructor = instructor;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var instructor = await _context.Instructors
            .Include(i => i.CourseTerms)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null)
        {
            return NotFound();
        }

        if (instructor.CourseTerms.Any())
        {
            ErrorMessage = "The instructor cannot be deleted while assigned to course terms.";
            Instructor = instructor;
            return Page();
        }

        _context.Instructors.Remove(instructor);
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private Task<InstructorModel?> LoadInstructorAsync(int id)
    {
        return _context.Instructors
            .AsNoTracking()
            .Include(i => i.CourseTerms)
            .FirstOrDefaultAsync(i => i.Id == id);
    }
}
