using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using InstructorModel = SysJaky_N.Models.Instructor;

namespace SysJaky_N.Pages.Admin.Instructors;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InstructorModel Instructor { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizeInput();

        if (!string.IsNullOrWhiteSpace(Instructor.FullName))
        {
            bool exists = await _context.Instructors
                .AnyAsync(i => i.FullName == Instructor.FullName);
            if (exists)
            {
                ModelState.AddModelError("Instructor.FullName", "An instructor with this name already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Instructors.Add(Instructor);
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
