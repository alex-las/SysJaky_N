using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RazorLight;
using SysJaky_N.Data;
using SysJaky_N.EmailTemplates.Models;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface INewsletterComposer
{
    Task<int> ComposeAndSendIssueAsync(int issueId, CancellationToken cancellationToken = default);
}

public sealed class NewsletterComposer : INewsletterComposer
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IRazorLightEngine _razorLightEngine;
    private readonly ILogger<NewsletterComposer> _logger;
    private const string NewsletterTemplateViewName = "NewsletterIssue.cshtml";

    public NewsletterComposer(
        ApplicationDbContext context,
        IEmailSender emailSender,
        IRazorLightEngine razorLightEngine,
        ILogger<NewsletterComposer> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _razorLightEngine = razorLightEngine;
        _logger = logger;
    }

    public async Task<int> ComposeAndSendIssueAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _context.NewsletterIssues
            .Include(i => i.NewsletterTemplate)
                .ThenInclude(template => template.Regions)
                    .ThenInclude(region => region.Category)
            .Include(i => i.Sections)
                .ThenInclude(section => section.NewsletterSection)
                .ThenInclude(section => section.Category)
            .Include(i => i.Sections)
                .ThenInclude(section => section.TemplateRegion)
            .Include(i => i.Categories)
                .ThenInclude(category => category.NewsletterSectionCategory)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken)
            .ConfigureAwait(false);

        if (issue is null)
        {
            throw new InvalidOperationException($"Newsletter issue {issueId} was not found.");
        }

        var now = DateTime.UtcNow;
        var allowedCategoryIds = issue.Categories
            .Select(c => c.NewsletterSectionCategoryId)
            .ToHashSet();

        var baseSections = issue.Sections
            .OrderBy(section => section.SortOrder)
            .Where(section => section.NewsletterSection.IsPublished)
            .Where(section => !string.IsNullOrWhiteSpace(section.NewsletterSection.HtmlContent))
            .Where(section => allowedCategoryIds.Contains(section.NewsletterSection.NewsletterSectionCategoryId))
            .ToList();

        if (baseSections.Count == 0)
        {
            _logger.LogWarning("Newsletter issue {IssueId} has no published sections to send.", issueId);
            return 0;
        }

        var subscribers = await _context.NewsletterSubscribers
            .AsNoTracking()
            .Include(subscriber => subscriber.PreferredCategories)
            .Where(subscriber => subscriber.ConfirmedAtUtc != null)
            .Where(subscriber => subscriber.ConsentGiven)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sentCount = 0;

        const int UnassignedRegionKey = -1;

        foreach (var subscriber in subscribers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subscriberCategoryIds = subscriber.PreferredCategories
                .Select(category => category.CourseCategoryId)
                .ToHashSet();

            var groupedSections = new Dictionary<int, List<(NewsletterIssueEmailSectionModel Section, string? CategoryName)>>();

            foreach (var issueSection in baseSections)
            {
                var section = issueSection.NewsletterSection;
                var category = section.Category;

                if (category is null)
                {
                    continue;
                }

                if (category.CourseCategoryId.HasValue && !subscriberCategoryIds.Contains(category.CourseCategoryId.Value))
                {
                    continue;
                }

                var emailSection = new NewsletterIssueEmailSectionModel(section.Title, section.HtmlContent);
                var key = issueSection.NewsletterTemplateRegionId ?? UnassignedRegionKey;

                if (!groupedSections.TryGetValue(key, out var list))
                {
                    list = new List<(NewsletterIssueEmailSectionModel, string?)>();
                    groupedSections[key] = list;
                }

                list.Add((emailSection, category.Name));
            }

            if (groupedSections.Count == 0)
            {
                continue;
            }

            var emailRegions = new List<NewsletterIssueEmailRegionModel>();
            var templateRegions = issue.NewsletterTemplate?.Regions
                .OrderBy(region => region.SortOrder)
                .ThenBy(region => region.Name)
                .ToList();

            if (templateRegions is not null)
            {
                foreach (var region in templateRegions)
                {
                    if (!groupedSections.TryGetValue(region.Id, out var sectionTuples) || sectionTuples.Count == 0)
                    {
                        continue;
                    }

                    emailRegions.Add(new NewsletterIssueEmailRegionModel
                    {
                        Name = region.Name,
                        CategoryName = region.Category?.Name,
                        Sections = sectionTuples.Select(tuple => tuple.Section).ToList()
                    });

                    groupedSections.Remove(region.Id);
                }
            }

            foreach (var leftover in groupedSections.Values)
            {
                foreach (var categoryGroup in leftover.GroupBy(tuple => tuple.CategoryName ?? "Obsah"))
                {
                    emailRegions.Add(new NewsletterIssueEmailRegionModel
                    {
                        Name = categoryGroup.Key,
                        CategoryName = categoryGroup.Key,
                        Sections = categoryGroup.Select(tuple => tuple.Section).ToList()
                    });
                }
            }

            if (emailRegions.Count == 0)
            {
                continue;
            }

            var templateModel = new NewsletterTemplateEmailModel
            {
                Name = issue.NewsletterTemplate?.Name ?? "Newsletter",
                PrimaryColor = issue.NewsletterTemplate?.PrimaryColor ?? "#2563eb",
                SecondaryColor = issue.NewsletterTemplate?.SecondaryColor ?? "#facc15",
                BackgroundColor = issue.NewsletterTemplate?.BackgroundColor ?? "#f9fafb",
                BaseLayoutHtml = issue.NewsletterTemplate?.BaseLayoutHtml ?? string.Empty
            };

            var model = new NewsletterIssueEmailModel
            {
                Subject = issue.Subject,
                Preheader = issue.Preheader,
                IntroHtml = issue.IntroHtml,
                OutroHtml = issue.OutroHtml,
                Template = templateModel,
                Regions = emailRegions
            };

            var delivery = new NewsletterIssueDelivery
            {
                NewsletterIssueId = issue.Id,
                NewsletterSubscriberId = subscriber.Id,
                RecipientEmail = subscriber.Email,
                SentUtc = DateTime.UtcNow,
                Status = NormalizeStatus("Pending")
            };

            _context.NewsletterIssueDeliveries.Add(delivery);

            var renderSucceeded = false;
            var renderedHtml = string.Empty;

            try
            {
                renderedHtml = await _razorLightEngine
                    .CompileRenderAsync(NewsletterTemplateViewName, model)
                    .ConfigureAwait(false);
                delivery.RenderedHtml = renderedHtml;
                renderSucceeded = true;
            }
            catch (Exception ex)
            {
                delivery.Status = NormalizeStatus($"Rendering failed: {ex.Message}");
                _logger.LogError(ex, "Failed to render newsletter issue {IssueId} for subscriber {SubscriberId}.", issueId, subscriber.Id);
                continue;
            }

            if (!renderSucceeded)
            {
                continue;
            }

            try
            {
                var log = await _emailSender
                    .SendEmailAsync(subscriber.Email, EmailTemplate.NewsletterIssue, model, cancellationToken, renderedHtml)
                    .ConfigureAwait(false);

                delivery.EmailLogId = log.Id;
                delivery.Status = log.Status;
                delivery.SentUtc = log.SentUtc;
                sentCount++;
            }
            catch (EmailSender.EmailSendException ex)
            {
                var log = ex.EmailLog;
                delivery.EmailLogId = log.Id;
                delivery.Status = log.Status;
                delivery.SentUtc = log.SentUtc;
                _logger.LogError(ex, "Failed to send newsletter issue {IssueId} to subscriber {SubscriberId}.", issueId, subscriber.Id);
            }
            catch (Exception ex)
            {
                delivery.Status = NormalizeStatus($"Failed to send: {ex.Message}");
                delivery.SentUtc = DateTime.UtcNow;
                _logger.LogError(ex, "Failed to send newsletter issue {IssueId} to subscriber {SubscriberId}.", issueId, subscriber.Id);
            }
        }

        if (sentCount > 0)
        {
            issue.SentAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return sentCount;
    }

    private static string NormalizeStatus(string status)
    {
        const int maxLength = 256;
        return status.Length <= maxLength ? status : status[..maxLength];
    }
}
