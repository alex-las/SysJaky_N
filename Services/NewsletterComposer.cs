using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<NewsletterComposer> _logger;

    public NewsletterComposer(ApplicationDbContext context, IEmailSender emailSender, ILogger<NewsletterComposer> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<int> ComposeAndSendIssueAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _context.NewsletterIssues
            .Include(i => i.Sections)
                .ThenInclude(section => section.NewsletterSection)
                .ThenInclude(section => section.Category)
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
            .Select(section => section.NewsletterSection)
            .Where(section => section.IsPublished)
            .Where(section => !string.IsNullOrWhiteSpace(section.HtmlContent))
            .Where(section => allowedCategoryIds.Contains(section.NewsletterSectionCategoryId))
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

        foreach (var subscriber in subscribers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subscriberCategoryIds = subscriber.PreferredCategories
                .Select(category => category.CourseCategoryId)
                .ToHashSet();

            var personalizedSections = new List<NewsletterIssueEmailSectionModel>();

            foreach (var section in baseSections)
            {
                var sectionCategory = section.Category;
                if (sectionCategory is null)
                {
                    continue;
                }

                if (sectionCategory.CourseCategoryId.HasValue && !subscriberCategoryIds.Contains(sectionCategory.CourseCategoryId.Value))
                {
                    continue;
                }

                personalizedSections.Add(new NewsletterIssueEmailSectionModel(
                    section.Title,
                    section.HtmlContent,
                    sectionCategory.Name));
            }

            if (personalizedSections.Count == 0)
            {
                continue;
            }

            var model = new NewsletterIssueEmailModel
            {
                Subject = issue.Subject,
                Preheader = issue.Preheader,
                IntroHtml = issue.IntroHtml,
                Sections = personalizedSections,
                OutroHtml = issue.OutroHtml
            };

            try
            {
                await _emailSender.SendEmailAsync(subscriber.Email, EmailTemplate.NewsletterIssue, model, cancellationToken)
                    .ConfigureAwait(false);
                sentCount++;
            }
            catch (Exception ex)
            {
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
}
