using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Account;

[Authorize]
public class ManageModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<ManageModel> _localizer;

    public ManageModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, IStringLocalizer<ManageModel> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _localizer = localizer;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public RedeemTokenInputModel RedeemTokenInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public List<Order> Orders { get; set; } = new();
    public List<Enrollment> Enrollments { get; private set; } = new();
    public List<OrderItem> UpcomingItems { get; private set; } = new();
    public List<WishlistItem> WishlistItems { get; private set; } = new();
    public List<WishlistItem> CompanyWishlistItems { get; private set; } = new();
    public string CalendarFeedUrl { get; private set; } = string.Empty;

    public class InputModel
    {
        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        public string? ReferenceCode { get; set; }
    }

    public class RedeemTokenInputModel
    {
        public string Token { get; set; } = string.Empty;
    }

    public CompanyProfile? Company { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            LocalizeValidationErrors();
            await LoadPageDataAsync(user, resetInput: false, resetRedeemInput: true);
            return Page();
        }

        user.Email = Input.Email;
        user.UserName = Input.Email;
        user.PhoneNumber = Input.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(Input.ReferenceCode))
        {
            var company = await _context.CompanyProfiles.FirstOrDefaultAsync(c => c.ReferenceCode == Input.ReferenceCode);
            if (company != null)
            {
                user.CompanyProfileId = company.Id;
            }
        }

        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = _localizer["ProfileUpdated"];

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRedeemTokenAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            LocalizeValidationErrors();
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        var tokenValue = RedeemTokenInput.Token?.Trim();
        if (string.IsNullOrEmpty(tokenValue))
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), _localizer["ErrorTokenRequired"]);
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        tokenValue = tokenValue.ToUpperInvariant();

        var seatToken = await _context.SeatTokens
            .Include(t => t.OrderItem)
                .ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(t => t.Token == tokenValue);

        if (seatToken == null)
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), _localizer["ErrorTokenNotFound"]);
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        if (seatToken.RedeemedAtUtc.HasValue)
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), _localizer["ErrorTokenAlreadyRedeemed"]);
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        var orderItem = seatToken.OrderItem;
        if (orderItem == null)
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), _localizer["ErrorTokenNotAssociated"]);
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var courseTerms = await _context.CourseTerms
            .Where(term => term.CourseId == orderItem.CourseId && term.IsActive)
            .OrderBy(term => term.StartUtc)
            .ToListAsync();

        CourseTerm? allocatedTerm = null;
        foreach (var term in courseTerms)
        {
            var availableSeats = term.Capacity - term.SeatsTaken;
            if (availableSeats <= 0)
            {
                continue;
            }

            term.SeatsTaken += 1;
            allocatedTerm = term;
            break;
        }

        if (allocatedTerm == null)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), _localizer["ErrorNoSeatsAvailable"]);
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        var enrollment = new Enrollment
        {
            UserId = user.Id,
            CourseTermId = allocatedTerm.Id,
            Status = EnrollmentStatus.Confirmed
        };

        _context.Enrollments.Add(enrollment);
        seatToken.RedeemedByUserId = user.Id;
        seatToken.RedeemedAtUtc = DateTime.UtcNow;
        seatToken.Token = tokenValue;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var courseTitle = orderItem.Course?.Title ?? _localizer["CourseFallback", orderItem.CourseId];
        var termDate = allocatedTerm.StartUtc.ToLocalTime().ToString("g");
        StatusMessage = _localizer["StatusTokenRedeemed", courseTitle, termDate];

        return RedirectToPage();
    }

    private void LocalizeValidationErrors()
    {
        ReplaceErrorMessage($"{nameof(Input)}.{nameof(InputModel.Email)}", static message => message.Contains("valid e-mail", StringComparison.OrdinalIgnoreCase), _localizer["ErrorEmailInvalid"]);
        ReplaceErrorMessage($"{nameof(Input)}.{nameof(InputModel.PhoneNumber)}", static message => message.Contains("valid phone", StringComparison.OrdinalIgnoreCase), _localizer["ErrorPhoneInvalid"]);
    }

    private void ReplaceErrorMessage(string key, Func<string, bool> predicate, string replacement)
    {
        if (!ModelState.TryGetValue(key, out var entry))
        {
            return;
        }

        var needsReplacement = entry.Errors.Any(error => predicate(error.ErrorMessage));
        if (!needsReplacement)
        {
            return;
        }

        entry.Errors.Clear();
        entry.Errors.Add(replacement);
    }

    private async Task LoadPageDataAsync(ApplicationUser user, bool resetInput, bool resetRedeemInput)
    {
        Company = await _context.CompanyProfiles.FirstOrDefaultAsync(c => c.Id == user.CompanyProfileId);

        if (resetInput)
        {
            Input = new InputModel
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                ReferenceCode = Company?.ReferenceCode
            };
        }

        Orders = await _context.Orders
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Status == EnrollmentStatus.Confirmed)
            .Include(e => e.CourseTerm)
                .ThenInclude(term => term.Course)
            .ToListAsync();

        Enrollments = enrollments
            .Where(e => e.CourseTerm != null && e.CourseTerm.Course != null)
            .OrderBy(e => e.CourseTerm?.StartUtc ?? DateTime.MaxValue)
            .ToList();

        UpcomingItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Course)
            .Where(oi => oi.Order != null
                && oi.Order.UserId == user.Id
                && oi.Course != null
                && oi.Course.Date >= DateTime.Today)
            .OrderBy(oi => oi.Course != null ? oi.Course.Date : DateTime.MaxValue)
            .ToListAsync();

        WishlistItems = await _context.WishlistItems
            .Include(w => w.Course)
            .Where(w => w.UserId == user.Id)
            .OrderBy(w => w.Course != null ? w.Course.Title : string.Empty)
            .ToListAsync();

        CompanyWishlistItems = await _context.WishlistItems
            .Include(w => w.Course)
            .Include(w => w.User)
            .Where(w => w.User.CompanyProfile != null && w.User.CompanyProfile.ManagerId == user.Id)
            .OrderBy(w => w.User != null ? w.User.Email : string.Empty)
            .ThenBy(w => w.Course != null ? w.Course.Title : string.Empty)
            .ToListAsync();

        var host = Request.Host.HasValue ? Request.Host.Value : "localhost";
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        CalendarFeedUrl = $"{Request.Scheme}://{host}{pathBase}/Account/Calendar/MyCourses.ics";

        if (resetRedeemInput)
        {
            RedeemTokenInput = new RedeemTokenInputModel();
        }
    }
}
