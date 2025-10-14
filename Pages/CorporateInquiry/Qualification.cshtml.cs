using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SysJaky_N.Resources;

namespace SysJaky_N.Pages.CorporateInquiry;

public class QualificationModel : PageModel
{
    private readonly ILogger<QualificationModel> _logger;
    private readonly IStringLocalizer<QualificationModel> _localizer;

    private static readonly (string Value, string ResourceKey)[] CompanySizeChoices =
    {
        ("micro", "CompanySizeMicro"),
        ("small", "CompanySizeSmall"),
        ("medium", "CompanySizeMedium"),
        ("large", "CompanySizeLarge"),
        ("enterprise", "CompanySizeEnterprise")
    };

    private static readonly string[] DefaultStandards =
    {
        "ISO 9001",
        "ISO 14001",
        "ISO 45001",
        "ISO 27001",
        "ISO 13485",
        "ISO 17025",
        "ISO 15189",
        "IATF 16949",
        "HACCP"
    };

    public QualificationModel(
        ILogger<QualificationModel> logger,
        IStringLocalizer<QualificationModel> localizer)
    {
        _logger = logger;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> CompanySizeOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> StandardOptions { get; private set; } = Array.Empty<SelectListItem>();

    public string? SuccessMessage { get; private set; }

    public void OnGet()
    {
        InitializeOptions();
        if (TempData.TryGetValue("QualificationSuccess", out var successObj) && successObj is string successText)
        {
            SuccessMessage = successText;
        }
    }

    public IActionResult OnPost()
    {
        InitializeOptions();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _logger.LogInformation("New corporate qualification lead {@Lead}", new
        {
            Input.CompanySize,
            Input.Standard,
            Input.DesiredDate,
            Input.Location
        });

        TempData["QualificationSuccess"] = _localizer["SuccessMessage"].Value;
        return RedirectToPage();
    }

    private void InitializeOptions()
    {
        CompanySizeOptions = CompanySizeChoices
            .Select(choice => new SelectListItem(_localizer[choice.ResourceKey].Value ?? choice.ResourceKey, choice.Value))
            .ToList();

        StandardOptions = DefaultStandards
            .Select(standard => new SelectListItem(standard, standard))
            .ToList();
    }

    public class InputModel
    {
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public string CompanySize { get; set; } = string.Empty;

        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public string Standard { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public DateTime? DesiredDate { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(200, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string Location { get; set; } = string.Empty;
    }
}
