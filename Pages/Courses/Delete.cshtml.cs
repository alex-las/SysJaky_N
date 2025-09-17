using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICourseMediaStorage _courseMediaStorage;
    private readonly ICacheService _cacheService;

    public DeleteModel(ApplicationDbContext context, ICourseMediaStorage courseMediaStorage, ICacheService cacheService)
    {
        _context = context;
        _courseMediaStorage = courseMediaStorage;
        _cacheService = cacheService;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Course? course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (course == null)
        {
            return NotFound();
        }
        Course = course;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Course? course = await _context.Courses.FindAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();

        _cacheService.InvalidateCourseList();
        _cacheService.InvalidateCourseDetail(course.Id);

        try
        {
            await _courseMediaStorage.DeleteCoverImageAsync(course.Id, HttpContext.RequestAborted);
        }
        catch (IOException)
        {
            // Ignore file system errors when removing the cover image; the database record has already been deleted.
        }
        return RedirectToPage("Index");
    }
}
