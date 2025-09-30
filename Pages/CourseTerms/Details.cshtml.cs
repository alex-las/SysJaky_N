using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.CourseTerms;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<DetailsModel> _localizer;

    public DetailsModel(ApplicationDbContext context, IStringLocalizer<DetailsModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public CourseTerm Term { get; private set; } = null!;

    public string CourseTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var term = await _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Instructor)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term == null)
        {
            return NotFound();
        }

        Term = term;
        CourseTitle = string.IsNullOrWhiteSpace(term.Course?.Title)
            ? _localizer["CourseTitleFallback", term.CourseId]
            : term.Course!.Title;

        return Page();
    }
}
