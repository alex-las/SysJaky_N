using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public CourseBlock CourseBlock { get; set; } = default!;

    [BindProperty]
    public List<int> SelectedCourseIds { get; set; } = new();

    public IList<Course> AvailableCourses { get; set; } = new List<Course>();

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (block == null) return NotFound();
        CourseBlock = block;
        SelectedCourseIds = block.Modules.Select(m => m.Id).ToList();
        AvailableCourses = await _context.Courses.ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var block = await _context.CourseBlocks
            .Include(b => b.Modules)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (block == null) return NotFound();
        AvailableCourses = await _context.Courses.ToListAsync();
        if (!ModelState.IsValid)
        {
            CourseBlock = block;
            return Page();
        }
        block.Title = CourseBlock.Title;
        block.Description = CourseBlock.Description;
        block.Price = CourseBlock.Price;
        foreach (var course in block.Modules)
        {
            course.CourseBlockId = null;
        }
        var courses = await _context.Courses.Where(c => SelectedCourseIds.Contains(c.Id)).ToListAsync();
        foreach (var course in courses)
        {
            course.CourseBlockId = block.Id;
        }
        await _context.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
