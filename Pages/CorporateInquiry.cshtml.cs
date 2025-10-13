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
    private static readonly string[] Step1TrainingKeys =
    {
        "ISO9001",
        "ISO14001",
        "ISO17025",
        "ISO15189",
        "HACCP",
        "ISO45001",
        "ISO27001",
        "IATF16949",
        "ISO13485"
    };

    private static readonly string[] ModeValues = new[] { "InPerson", "Online", "Hybrid" };

    private static readonly string[] ServiceTypeKeys =
    {
        "CustomTraining",
        "CertificationProject",
        "PreAudit",
        "InternalAudit",
        "Consulting"
    };

    private static readonly string[] TrainingLevelKeys = { "Basic", "Advanced", "Certification" };

    private static readonly Dictionary<string, decimal> ServiceTypeMultipliers = new()
    {
        ["CustomTraining"] = 1m,
        ["CertificationProject"] = 1.6m,
        ["PreAudit"] = 1.35m,
        ["InternalAudit"] = 1.25m,
        ["Consulting"] = 1.15m
    };

    private static readonly Dictionary<string, decimal> TrainingLevelMultipliers = new()
    {
        ["Basic"] = 1m,
        ["Advanced"] = 1.2m,
        ["Certification"] = 1.45m
    };
    private readonly IStringLocalizer<CorporateInquiryModel> _localizer;

    public CorporateInquiryModel(IStringLocalizer<CorporateInquiryModel> localizer)
    {
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int InitialStep { get; private set; } = 1;

    public IReadOnlyList<string> IsoTrainingOptionKeys => Step1TrainingKeys;

    public IReadOnlyList<string> ServiceTypeOptions => ServiceTypeKeys;

    public IReadOnlyList<string> TrainingLevelOptions => TrainingLevelKeys;

    public IReadOnlyList<string> ModeOptions => ModeValues;

    public decimal BaseTrainingPrice => 8500m;

    public decimal ParticipantPrice => 950m;

    public string ModeMultipliersJson => JsonSerializer.Serialize(new Dictionary<string, decimal>
    {
        ["InPerson"] = 1.25m,
        ["Online"] = 1m,
        ["Hybrid"] = 1.4m
    });

    public string ServiceTypeMultipliersJson => JsonSerializer.Serialize(ServiceTypeMultipliers);

    public string TrainingLevelMultipliersJson => JsonSerializer.Serialize(TrainingLevelMultipliers);

    public class InputModel
    {
        [Display(Name = nameof(CorporateInquiryResources.ServiceTypeLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public string ServiceType { get; set; } = string.Empty;

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

        [Display(Name = nameof(CorporateInquiryResources.TrainingLevelLabel), ResourceType = typeof(CorporateInquiryResources))]
        [Required(ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.FieldRequired))]
        public string TrainingLevel { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.LocationLabel), ResourceType = typeof(CorporateInquiryResources))]
        [StringLength(200, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string Location { get; set; } = string.Empty;

        [Display(Name = nameof(CorporateInquiryResources.SpecialRequirementsLabel), ResourceType = typeof(CorporateInquiryResources))]
        [StringLength(1000, ErrorMessageResourceType = typeof(SharedResources), ErrorMessageResourceName = nameof(SharedResources.StringLength))]
        public string SpecialRequirements { get; set; } = string.Empty;

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
            ModelState.AddModelError("Input.TrainingTypes", _localizer["ValidationTrainingTypesRequired"]);
        }

        if (RequiresLocation(Input.Mode) && string.IsNullOrWhiteSpace(Input.Location))
        {
            ModelState.AddModelError("Input.Location", _localizer["ValidationLocationRequired"]);
        }

        if (!ModelState.IsValid)
        {
            InitialStep = DetermineFirstInvalidStep();
            return Page();
        }

        TempData["Success"] = _localizer["SuccessMessage"].Value;
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

            if (entry.Key.StartsWith("Input.ServiceType", StringComparison.Ordinal))
            {
                return 1;
            }

            if (entry.Key.StartsWith("Input.TrainingTypes", StringComparison.Ordinal))
            {
                return 2;
            }

            if (entry.Key.StartsWith("Input.ParticipantCount", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.PreferredDate", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.Mode", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.TrainingLevel", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.Location", StringComparison.Ordinal))
            {
                return 3;
            }
            if (entry.Key.StartsWith("Input.CompanyId", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.CompanyName", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactPerson", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactEmail", StringComparison.Ordinal) ||
                entry.Key.StartsWith("Input.ContactPhone", StringComparison.Ordinal))
            {
                return 4;
            }
        }

        return 1;
    }

    private static bool RequiresLocation(string mode)
    {
        return string.Equals(mode, "InPerson", StringComparison.Ordinal) ||
               string.Equals(mode, "Hybrid", StringComparison.Ordinal);
    }
}
