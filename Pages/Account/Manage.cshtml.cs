using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Account;

[Authorize]
public class ManageModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public ManageModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public RedeemTokenInputModel RedeemTokenInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public List<Order> Orders { get; set; } = new();
    public List<Enrollment> Enrollments { get; private set; } = new();
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
        [Required]
        [Display(Name = "Seat token")]
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
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        var tokenValue = RedeemTokenInput.Token?.Trim();
        if (string.IsNullOrEmpty(tokenValue))
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), "Token is required.");
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
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), "Token was not found.");
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        if (seatToken.RedeemedAtUtc.HasValue)
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), "This token has already been redeemed.");
            await LoadPageDataAsync(user, resetInput: true, resetRedeemInput: false);
            return Page();
        }

        var orderItem = seatToken.OrderItem;
        if (orderItem == null)
        {
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), "Token is not associated with a course item.");
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
            ModelState.AddModelError(nameof(RedeemTokenInput.Token), "No seats are currently available for this course.");
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

        var courseTitle = orderItem.Course?.Title ?? $"Course {orderItem.CourseId}";
        var termDate = allocatedTerm.StartUtc.ToLocalTime().ToString("g");
        StatusMessage = $"Token redeemed for {courseTitle} ({termDate}).";

        return RedirectToPage();
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

        var host = Request.Host.HasValue ? Request.Host.Value : "localhost";
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        CalendarFeedUrl = $"{Request.Scheme}://{host}{pathBase}/Account/Calendar/MyCourses.ics";

        if (resetRedeemInput)
        {
            RedeemTokenInput = new RedeemTokenInputModel();
        }
    }
}
