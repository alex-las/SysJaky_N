using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CreateModel> _logger;
    private readonly IStringLocalizer<CreateModel> _localizer;
    private readonly ICacheService _cacheService;

    public CreateModel(
        ApplicationDbContext context,
        IEmailSender emailSender,
        ILogger<CreateModel> logger,
        ICacheService cacheService,
        IStringLocalizer<CreateModel> localizer)
    {
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
        _cacheService = cacheService;
        _localizer = localizer;
        Input.StartUtc = DateTime.UtcNow.ToLocalTime();
        Input.EndUtc = Input.StartUtc.AddHours(1);
    }

    [BindProperty]
    public CourseTermInputModel Input { get; set; } = new();

    public List<SelectListItem> CourseOptions { get; set; } = new();

    public List<SelectListItem> InstructorOptions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = _localizer["Title"];
        await LoadSelectListsAsync();

        var startUtc = DateTime.SpecifyKind(Input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(Input.EndUtc, DateTimeKind.Local).ToUniversalTime();

        if (endUtc <= startUtc)
        {
            ModelState.AddModelError("Input.EndUtc", _localizer["ErrorEndUtcBeforeStart"].Value);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == Input.CourseId);

        if (course == null)
        {
            ModelState.AddModelError("Input.CourseId", _localizer["ErrorCourseNotFound"].Value);
            return Page();
        }

        var term = new CourseTerm
        {
            CourseId = Input.CourseId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Capacity = Input.Capacity,
            SeatsTaken = 0,
            IsActive = Input.IsActive,
            InstructorId = Input.InstructorId
        };

        _context.CourseTerms.Add(term);
        await _context.SaveChangesAsync();

        _cacheService.InvalidateCourseList();
        _cacheService.InvalidateCourseDetail(term.CourseId);

        await NotifyWishlistUsersAsync(term, course);

        return RedirectToPage("Index");
    }

    private async Task NotifyWishlistUsersAsync(CourseTerm term, Course course)
    {
        var wishlistedUsers = await _context.WishlistItems
            .AsNoTracking()
            .Where(w => w.CourseId == course.Id)
            .Select(w => w.User)
            .OfType<ApplicationUser>()
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .ToListAsync();

        if (wishlistedUsers.Count == 0)
        {
            _logger.LogInformation(
                _localizer["LogNoWishlistUsers"],
                course.Id);
            return;
        }

        var host = Request.Host.HasValue ? Request.Host.Value : null;
        var detailUrl = Url.Page(
            "/CourseTerms/Details",
            pageHandler: null,
            values: new { id = term.Id },
            protocol: Request.Scheme,
            host: host);

        if (string.IsNullOrWhiteSpace(detailUrl))
        {
            detailUrl = $"/CourseTerms/Details/{term.Id}";
            _logger.LogWarning(
                _localizer["LogRelativeDetailUrl"],
                term.Id,
                detailUrl);
        }

        var courseTitle = string.IsNullOrWhiteSpace(course.Title)
            ? _localizer["CourseFallback", course.Id].Value
            : course.Title;

        _logger.LogInformation(
            _localizer["LogSendingNotification"],
            term.Id,
            courseTitle,
            wishlistedUsers.Count);

        foreach (var user in wishlistedUsers)
        {
            var email = user.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var model = new CourseTermCreatedEmailModel(
                courseTitle,
                term.StartUtc,
                term.EndUtc,
                detailUrl);

            try
            {
                await _emailSender.SendEmailAsync(email, EmailTemplate.CourseTermCreated, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    _localizer["LogSendEmailFailed"],
                    term.Id,
                    email);
            }
        }
    }

    private async Task LoadSelectListsAsync()
    {
        CourseOptions = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), Input.CourseId == c.Id))
            .ToListAsync();

        var instructorItems = await _context.Instructors
            .AsNoTracking()
            .OrderBy(i => i.FullName)
            .Select(i => new SelectListItem(i.FullName, i.Id.ToString(), Input.InstructorId == i.Id))
            .ToListAsync();

        InstructorOptions = new List<SelectListItem>
        {
            new(_localizer["SelectOptionUnassigned"].Value, string.Empty, Input.InstructorId == null)
        };
        InstructorOptions.AddRange(instructorItems);
    }
}
