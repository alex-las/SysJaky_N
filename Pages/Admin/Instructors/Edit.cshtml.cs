using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using InstructorModel = SysJaky_N.Models.Instructor;

namespace SysJaky_N.Pages.Admin.Instructors;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InstructorModel Instructor { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null)
        {
            return NotFound();
        }

        Instructor = instructor;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var instructorToUpdate = await _context.Instructors.FindAsync(id);
        if (instructorToUpdate == null)
        {
            return NotFound();
        }

        NormalizeInput();

        if (!string.IsNullOrWhiteSpace(Instructor.FullName))
        {
            bool exists = await _context.Instructors
                .AnyAsync(i => i.Id != id && i.FullName == Instructor.FullName);
            if (exists)
            {
                ModelState.AddModelError("Instructor.FullName", "An instructor with this name already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        instructorToUpdate.FullName = Instructor.FullName;
        instructorToUpdate.Email = Instructor.Email;
        instructorToUpdate.PhoneNumber = Instructor.PhoneNumber;
        instructorToUpdate.Bio = Instructor.Bio;

        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private void NormalizeInput()
    {
        Instructor.FullName = Instructor.FullName.Trim();

        if (string.IsNullOrWhiteSpace(Instructor.Email))
        {
            Instructor.Email = null;
        }
        else
        {
            Instructor.Email = Instructor.Email.Trim();
        }

        if (string.IsNullOrWhiteSpace(Instructor.PhoneNumber))
        {
            Instructor.PhoneNumber = null;
        }
        else
        {
            Instructor.PhoneNumber = Instructor.PhoneNumber.Trim();
        }

        if (string.IsNullOrWhiteSpace(Instructor.Bio))
        {
            Instructor.Bio = null;
        }
    }
}
