using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;

namespace SysJaky_N.Pages.Courses;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICourseEditor _courseEditor;
    private readonly IStringLocalizer<CreateModel> _localizer;

    public IStringLocalizer<CreateModel> Localizer => _localizer;

    public CreateModel(
        ApplicationDbContext context,
        IAuditService auditService,
        ICourseEditor courseEditor,
        IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _auditService = auditService;
        _courseEditor = courseEditor;
        _localizer = localizer;
    }

    [BindProperty]
    public Course Course { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverImage { get; set; }

    public SelectList CourseGroups { get; set; } = default!;

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    [BindProperty]
    public List<int> SelectedCategoryIds { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadSelectionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _courseEditor.ValidateCoverImage(CoverImage, ModelState, nameof(CoverImage));

        await LoadSelectionsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var categories = await _context.CourseCategories
            .Where(category => SelectedCategoryIds.Contains(category.Id))
            .ToListAsync();

        foreach (var category in categories)
        {
            Course.Categories.Add(category);
        }

        _context.Courses.Add(Course);
        await _context.SaveChangesAsync();

        var coverResult = await _courseEditor.SaveCoverImageAsync(
            Course.Id,
            CoverImage,
            HttpContext.RequestAborted);

        if (!coverResult.Succeeded)
        {
            await transaction.RollbackAsync();
            Course.Id = 0;
            ModelState.AddModelError(nameof(CoverImage), coverResult.ErrorMessage ?? string.Empty);
            return Page();
        }

        if (coverResult.CoverImageUrl is { } coverUrl)
        {
            Course.CoverImageUrl = coverUrl;
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        _courseEditor.InvalidateCourseCache(Course.Id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditService.LogAsync(userId, "CourseCreated", $"Course {Course.Id} created");
        return RedirectToPage("Index");
    }

    private async Task LoadSelectionsAsync()
    {
        var groups = await _context.CourseGroups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync();

        CourseGroups = new SelectList(groups, "Id", "Name", Course.CourseGroupId);

        SelectedCategoryIds ??= new List<int>();
        var selectedSet = new HashSet<int>(SelectedCategoryIds);

        var categories = await _context.CourseCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync();

        CategoryOptions = categories
            .Select(category => new SelectListItem(category.Name, category.Id.ToString(), selectedSet.Contains(category.Id)))
            .ToList();
    }
}
