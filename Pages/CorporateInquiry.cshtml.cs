using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SysJaky_N.Resources;

namespace SysJaky_N.Pages;

public class CorporateInquiryModel : PageModel
{
    private static readonly string[] Step1TrainingKeys = new[] { "ISO9001", "ISO14001", "ISO27001", "ISO45001" };
    private static readonly string[] ModeValues = new[] { "InPerson", "Online", "Hybrid" };
    private readonly IStringLocalizer<CorporateInquiryModel> _localizer;

    public CorporateInquiryModel(IStringLocalizer<CorporateInquiryModel> localizer)
    {
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int InitialStep { get; private set; } = 1;

    public IReadOnlyList<string> IsoTrainingOptionKeys => Step1TrainingKeys;

    public IReadOnlyList<string> ModeOptions => ModeValues;

    public decimal BaseTrainingPrice => 8500m;

    public decimal ParticipantPrice => 950m;

    public string ModeMultipliersJson => JsonSerializer.Serialize(new Dictionary<string, decimal>
    {
        ["InPerson"] = 1.25m,
        ["Online"] = 1m,
        ["Hybrid"] = 1.4m
    });

    public class InputModel
    {
        [Display(Name = nameof(CorporateInquiryResources.TrainingTypesLabel), ResourceType = typeof(CorporateInquiryResources))]
        public List<string> TrainingTypes { get; set; } = new();

        [Display(Name = nameof(CorporateInquiryResources.ParticipantCountLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [Range(1, 1000, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.Range))]
        public int? ParticipantCount { get; set; }

        [Display(Name = nameof(CorporateInquiryResources.PreferredDateLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [DataType(DataType.Date)]
        public DateTime? PreferredDate { get; set; }

        [Display(Name = nameof(CorporateInquiryResources.ModeLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public string Mode { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.CompanyIdLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(32, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string CompanyId { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.CompanyNameLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(200, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.ContactPersonLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [StringLength(120, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string ContactPerson { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.ContactEmailLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [EmailAddress(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.EmailAddressInvalid))]
        [StringLength(200, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string ContactEmail { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.ContactPhoneLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        [Phone(ErrorMessageResourceType = typeof(CorporateInquiryResources), ErrorMessageResourceName = nameof(CorporateInquiryResources.PhoneInvalid))]
        [StringLength(50, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string ContactPhone { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        Input.TrainingTypes ??= new List<string>();

        if (!Input.TrainingTypes.Any())
        {
            ModelState.AddModelError("Input.TrainingTypes", _localizer["ValidationStep1"]);
        }

        if (!ModelState.IsValid)
        {
            InitialStep = DetermineFirstInvalidStep();
            return Page();
        }

        TempData["Success"] = _localizer["SuccessMessage"];
        InitialStep = 1;
        return RedirectToPage();
    }

    private int DetermineFirstInvalidStep()
    {
        foreach (var entry in ModelState)
        {
            if (entry.Value.Errors.Count == 0)
            {
                continue;
            }

            if (entry.Key.StartsWith("Input.TrainingTypes", StringComparison.Ordinal))
            {
                return 1;
            }
            if (entry.Key.StartsWith("Input.ParticipantCount", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.PreferredDate", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.Mode", StringComparison.Ordinal))
            {
                return 2;
            }
            if (entry.Key.StartsWith("Input.CompanyId", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.CompanyName", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactPerson", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactEmail", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactPhone", StringComparison.Ordinal))
            {
                return 3;
            }
        }

        return 1;
    }
}
