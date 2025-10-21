using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Newsletters.Templates;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public RegionInputModel RegionInput { get; set; } = new();

    public NewsletterTemplate? Template { get; private set; }

    public IList<SelectListItem> CategoryOptions { get; private set; } = new List<SelectListItem>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(Input.Id, cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var template = await _context.NewsletterTemplates
            .FirstOrDefaultAsync(t => t.Id == Input.Id, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return NotFound();
        }

        template.Name = Input.Name.Trim();
        template.PrimaryColor = NormalizeColor(Input.PrimaryColor, "#2563eb");
        template.SecondaryColor = NormalizeColor(Input.SecondaryColor, "#facc15");
        template.BackgroundColor = NormalizeColor(Input.BackgroundColor, "#f9fafb");
        template.BaseLayoutHtml = (Input.BaseLayoutHtml ?? string.Empty).Trim();
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Šablona byla aktualizována.";
        return RedirectToPage(new { id = template.Id });
    }

    public async Task<IActionResult> OnPostAddRegionAsync(int templateId, CancellationToken cancellationToken)
    {
        RemoveTemplateValidation();
        if (!TryValidateModel(RegionInput, nameof(RegionInput)))
        {
            await LoadAsync(templateId, cancellationToken).ConfigureAwait(false);
            return Page();
        }

        if (!await _context.NewsletterSectionCategories.AnyAsync(c => c.Id == RegionInput.NewsletterSectionCategoryId, cancellationToken).ConfigureAwait(false))
        {
            ModelState.AddModelError("RegionInput.NewsletterSectionCategoryId", "Zvolená kategorie neexistuje.");
            await LoadAsync(templateId, cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var template = await _context.NewsletterTemplates
            .Include(t => t.Regions)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return NotFound();
        }

        var sortOrder = RegionInput.SortOrder;
        if (sortOrder <= 0)
        {
            sortOrder = template.Regions.Count == 0 ? 0 : template.Regions.Max(r => r.SortOrder) + 1;
        }

        var region = new NewsletterTemplateRegion
        {
            Name = RegionInput.Name.Trim(),
            SortOrder = sortOrder,
            NewsletterSectionCategoryId = RegionInput.NewsletterSectionCategoryId
        };

        template.Regions.Add(region);
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Oblast byla přidána.";
        return RedirectToPage(new { id = templateId });
    }

    public async Task<IActionResult> OnPostUpdateRegionAsync(int templateId, CancellationToken cancellationToken)
    {
        RemoveTemplateValidation();
        if (!TryValidateModel(RegionInput, nameof(RegionInput)))
        {
            await LoadAsync(templateId, cancellationToken).ConfigureAwait(false);
            return Page();
        }

        if (!RegionInput.Id.HasValue)
        {
            return BadRequest();
        }

        if (!await _context.NewsletterSectionCategories.AnyAsync(c => c.Id == RegionInput.NewsletterSectionCategoryId, cancellationToken).ConfigureAwait(false))
        {
            ModelState.AddModelError("RegionInput.NewsletterSectionCategoryId", "Zvolená kategorie neexistuje.");
            await LoadAsync(templateId, cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var region = await _context.NewsletterTemplateRegions
            .Include(r => r.NewsletterTemplate)
            .FirstOrDefaultAsync(r => r.Id == RegionInput.Id.Value && r.NewsletterTemplateId == templateId, cancellationToken)
            .ConfigureAwait(false);

        if (region is null)
        {
            return NotFound();
        }

        region.Name = RegionInput.Name.Trim();
        region.SortOrder = RegionInput.SortOrder < 0 ? 0 : RegionInput.SortOrder;
        region.NewsletterSectionCategoryId = RegionInput.NewsletterSectionCategoryId;
        region.NewsletterTemplate.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Oblast byla aktualizována.";
        return RedirectToPage(new { id = templateId });
    }

    public async Task<IActionResult> OnPostDeleteRegionAsync(int templateId, int id, CancellationToken cancellationToken)
    {
        var region = await _context.NewsletterTemplateRegions
            .Include(r => r.NewsletterTemplate)
            .FirstOrDefaultAsync(r => r.Id == id && r.NewsletterTemplateId == templateId, cancellationToken)
            .ConfigureAwait(false);

        if (region is null)
        {
            return NotFound();
        }

        _context.NewsletterTemplateRegions.Remove(region);
        region.NewsletterTemplate.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        StatusMessage = "Oblast byla odstraněna.";
        return RedirectToPage(new { id = templateId });
    }

    private async Task<bool> LoadAsync(int id, CancellationToken cancellationToken)
    {
        Template = await _context.NewsletterTemplates
            .Include(template => template.Regions)
                .ThenInclude(region => region.Category)
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken)
            .ConfigureAwait(false) ?? null;

        if (Template is null)
        {
            return false;
        }

        Input = new InputModel
        {
            Id = Template.Id,
            Name = Template.Name,
            PrimaryColor = Template.PrimaryColor,
            SecondaryColor = Template.SecondaryColor,
            BackgroundColor = Template.BackgroundColor,
            BaseLayoutHtml = Template.BaseLayoutHtml
        };

        CategoryOptions = await _context.NewsletterSectionCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private void RemoveTemplateValidation()
    {
        ModelState.Remove("Input.Name");
        ModelState.Remove("Input.PrimaryColor");
        ModelState.Remove("Input.SecondaryColor");
        ModelState.Remove("Input.BackgroundColor");
        ModelState.Remove("Input.BaseLayoutHtml");
    }

    private static string NormalizeColor(string? input, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
        return value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value;
    }

    public sealed class InputModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        [Display(Name = "Název")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Primární barva")]
        public string PrimaryColor { get; set; } = "#2563eb";

        [Display(Name = "Druhá barva")]
        public string SecondaryColor { get; set; } = "#facc15";

        [Display(Name = "Barva pozadí")]
        public string BackgroundColor { get; set; } = "#f9fafb";

        [Display(Name = "HTML rozložení")]
        public string BaseLayoutHtml { get; set; } = string.Empty;
    }

    public sealed class RegionInputModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(128)]
        [Display(Name = "Název")]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        [Display(Name = "Pořadí")]
        public int SortOrder { get; set; }

        [Required]
        [Display(Name = "Kategorie sekcí")]
        public int NewsletterSectionCategoryId { get; set; }
    }
}
