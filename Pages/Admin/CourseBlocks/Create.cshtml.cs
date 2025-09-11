using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public CourseBlock CourseBlock { get; set; } = new();

    [BindProperty]
    public List<int> SelectedCourseIds { get; set; } = new();

    public IList<Course> AvailableCourses { get; set; } = new List<Course>();

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        AvailableCourses = await _context.Courses.ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AvailableCourses = await _context.Courses.ToListAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }
        _context.CourseBlocks.Add(CourseBlock);
        await _context.SaveChangesAsync();
        var courses = await _context.Courses.Where(c => SelectedCourseIds.Contains(c.Id)).ToListAsync();
        foreach (var course in courses)
        {
            course.CourseBlockId = CourseBlock.Id;
        }
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
