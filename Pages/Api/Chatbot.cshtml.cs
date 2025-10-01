using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Api;

[IgnoreAntiforgeryToken(Order = 1001)]
public class ChatbotModel : PageModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<ChatbotKeywordEntry> _keywords;

    public ChatbotModel(
        ApplicationDbContext context,
        IEmailSender emailSender,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _keywords = LoadKeywordEntries(environment);
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return new JsonResult(new ChatbotResponse
            {
                Reply = "Virtuální poradce je momentálně vypnutý. Zkuste to prosím později."
            });
        }

        var request = await JsonSerializer.DeserializeAsync<ChatbotRequest>(Request.Body, SerializerOptions, cancellationToken);
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return new JsonResult(new ChatbotResponse
            {
                Reply = "Omlouvám se, ale nerozuměl jsem dotazu. Můžete ho prosím zopakovat?"
            });
        }

        var normalizedMessage = request.Message.Trim();
        var matchedKeyword = FindMatchingKeyword(normalizedMessage);

        var recommendations = await BuildCourseRecommendationsAsync(normalizedMessage, matchedKeyword, cancellationToken);

        var response = new ChatbotResponse
        {
            Reply = matchedKeyword?.Response
                ?? (recommendations.Count > 0
                    ? "Vybral jsem pro vás několik vhodných kurzů."
                    : "Momentálně jsem nenašel přesnou shodu. Můžete dotaz prosím upřesnit?"),
            Courses = recommendations,
            FollowUp = matchedKeyword?.FollowUp
                ?? (recommendations.Count == 0
                    ? "Zkuste prosím přidat oblast, která vás zajímá, nebo napište klíčové slovo kurzu."
                    : null)
        };

        return new JsonResult(response);
    }

    public async Task<IActionResult> OnPostEscalateAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return new JsonResult(new ChatbotResponse
            {
                Reply = "Virtuální poradce je momentálně vypnutý. Prosím kontaktujte nás jinou cestou."
            });
        }

        var request = await JsonSerializer.DeserializeAsync<EscalationRequest>(Request.Body, SerializerOptions, cancellationToken);
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "E-mail je povinný." });
        }

        var email = request.Email.Trim();
        var transcript = BuildTranscript(request.History);

        var contactMessage = new ContactMessage
        {
            Name = "Chatbot klient",
            Email = email,
            Message = transcript,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contactMessage);
        await _context.SaveChangesAsync(cancellationToken);

        var adminEmail = _configuration["SeedAdmin:Email"];
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            await _emailSender.SendEmailAsync(
                adminEmail,
                EmailTemplate.ContactMessageNotification,
                new ContactMessageEmailModel("Chatbot klient", email, transcript),
                cancellationToken);
        }

        return new JsonResult(new ChatbotResponse
        {
            Reply = "Děkujeme, předali jsme váš dotaz kolegům. Ozvou se vám na uvedený e-mail co nejdříve."
        });
    }

    private async Task<ChatbotSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _context.ChatbotSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return settings ?? new ChatbotSettings
        {
            IsEnabled = true,
            AutoInitialize = true,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private ChatbotKeywordEntry? FindMatchingKeyword(string message)
    {
        return _keywords.FirstOrDefault(entry => entry.IsMatch(message));
    }

    private async Task<List<CourseRecommendation>> BuildCourseRecommendationsAsync(
        string message,
        ChatbotKeywordEntry? matchedKeyword,
        CancellationToken cancellationToken)
    {
        var allCourses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CourseSnapshot(c.Id, c.Title, c.Price, c.Date))
            .ToListAsync(cancellationToken);

        var candidates = new List<CourseSnapshot>();
        var messageTokens = message
            .Split(new[] { ' ', ',', ';', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 2)
            .ToArray();

        if (matchedKeyword?.CourseTitleContains?.Length > 0)
        {
            foreach (var course in allCourses)
            {
                if (matchedKeyword.CourseTitleContains.Any(filter =>
                        course.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    AddCandidate(candidates, course);
                }
            }
        }

        if (candidates.Count == 0 && matchedKeyword is not null)
        {
            foreach (var keyword in matchedKeyword.Keywords)
            {
                foreach (var course in allCourses)
                {
                    if (course.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        AddCandidate(candidates, course);
                    }
                }
            }
        }

        if (candidates.Count == 0 && messageTokens.Length > 0)
        {
            foreach (var token in messageTokens)
            {
                foreach (var course in allCourses)
                {
                    if (course.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        AddCandidate(candidates, course);
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(allCourses.OrderBy(course => course.Date).Take(3));
        }

        var courseIds = candidates.Select(course => course.Id).Distinct().ToArray();
        var upcomingTerms = await _context.CourseTerms
            .AsNoTracking()
            .Where(term => courseIds.Contains(term.CourseId) && term.IsActive)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var nextTerms = upcomingTerms
            .GroupBy(term => term.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(term => term.StartUtc >= now)
                    .OrderBy(term => term.StartUtc)
                    .FirstOrDefault()
                    ?? group.OrderBy(term => term.StartUtc).FirstOrDefault());

        var recommendations = candidates
            .DistinctBy(course => course.Id)
            .Select(course => new CourseRecommendation
            {
                Id = course.Id,
                Title = course.Title,
                Price = course.Price,
                NextTermStart = nextTerms.TryGetValue(course.Id, out var term) ? term?.StartUtc : null,
                DetailUrl = Url.Page("/Courses/Details", new { id = course.Id }) ?? string.Empty
            })
            .ToList();

        if (matchedKeyword?.SortByNextTerm is true)
        {
            recommendations = recommendations
                .OrderBy(rec => rec.NextTermStart ?? DateTime.MaxValue)
                .ThenBy(rec => rec.Title)
                .Take(4)
                .ToList();
        }
        else
        {
            recommendations = recommendations
                .OrderBy(rec => rec.Title)
                .Take(4)
                .ToList();
        }

        return recommendations;
    }

    private static void AddCandidate(ICollection<CourseSnapshot> candidates, CourseSnapshot course)
    {
        if (candidates.Any(candidate => candidate.Id == course.Id))
        {
            return;
        }

        candidates.Add(course);
    }

    private static IReadOnlyList<ChatbotKeywordEntry> LoadKeywordEntries(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "ChatbotKeywords.json");
        if (!System.IO.File.Exists(path))
        {
            return Array.Empty<ChatbotKeywordEntry>();
        }

        using var stream = System.IO.File.OpenRead(path);
        var entries = JsonSerializer.Deserialize<List<ChatbotKeywordEntry>>(stream, SerializerOptions) ?? new List<ChatbotKeywordEntry>();
        return entries;
    }

    private static string BuildTranscript(IReadOnlyCollection<ChatbotHistoryMessage>? history)
    {
        if (history is null || history.Count == 0)
        {
            return "Zákazník požádal o kontakt prostřednictvím chatbota.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Přepis konverzace z chatbota:");

        foreach (var message in history)
        {
            var role = message.Role switch
            {
                "assistant" => "Poradce",
                "user" => "Zákazník",
                _ => "Systém"
            };

            builder.AppendLine($"{role}: {message.Content}");
        }

        return builder.ToString();
    }

    private sealed record CourseSnapshot(int Id, string Title, decimal Price, DateTime Date);

    private sealed record ChatbotKeywordEntry
    {
        public string[] Keywords { get; init; } = Array.Empty<string>();
        public string? Response { get; init; }
        public string[]? CourseTitleContains { get; init; }
        public bool SortByNextTerm { get; init; }
        public string? FollowUp { get; init; }

        public bool IsMatch(string message)
        {
            foreach (var keyword in Keywords)
            {
                if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed record ChatbotRequest
    {
        public string Message { get; init; } = string.Empty;
        public IReadOnlyList<ChatbotHistoryMessage>? History { get; init; }
    }

    private sealed record EscalationRequest
    {
        public string Email { get; init; } = string.Empty;
        public IReadOnlyList<ChatbotHistoryMessage>? History { get; init; }
    }

    private sealed record ChatbotHistoryMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string? Type { get; init; }
    }

    private sealed record ChatbotResponse
    {
        public string Reply { get; init; } = string.Empty;
        public List<CourseRecommendation> Courses { get; init; } = new();
        public string? FollowUp { get; init; }
    }

    private sealed record CourseRecommendation
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public DateTime? NextTermStart { get; init; }
        public string DetailUrl { get; init; } = string.Empty;
    }
}
