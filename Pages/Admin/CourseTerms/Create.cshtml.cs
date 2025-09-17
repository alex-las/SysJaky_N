using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(ApplicationDbContext context, IEmailSender emailSender, ILogger<CreateModel> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
        Input.StartUtc = DateTime.UtcNow.ToLocalTime();
        Input.EndUtc = Input.StartUtc.AddHours(1);
    }

    [BindProperty]
    public CourseTermInputModel Input { get; set; } = new();

    public List<SelectListItem> CourseOptions { get; set; } = new();

    public List<SelectListItem> InstructorOptions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSelectListsAsync();

        var startUtc = DateTime.SpecifyKind(Input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(Input.EndUtc, DateTimeKind.Local).ToUniversalTime();

        if (endUtc <= startUtc)
        {
            ModelState.AddModelError("Input.EndUtc", "End time must be after start time.");
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
            ModelState.AddModelError("Input.CourseId", "Selected course was not found.");
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
                "Žádní uživatelé ve wishlistu pro kurz {CourseId}, oznámení nebudou odeslána.",
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
                "Nepodařilo se vygenerovat absolutní odkaz na termín {TermId}. Používám relativní cestu {Url}.",
                term.Id,
                detailUrl);
        }

        var courseTitle = string.IsNullOrWhiteSpace(course.Title) ? $"Kurz {course.Id}" : course.Title;

        _logger.LogInformation(
            "Odesílám oznámení o novém termínu {TermId} ({CourseTitle}) {RecipientCount} uživatelům.",
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
                    "Odeslání oznámení o termínu {TermId} uživateli {Email} selhalo.",
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
            new("Unassigned", string.Empty, Input.InstructorId == null)
        };
        InstructorOptions.AddRange(instructorItems);
    }
}
