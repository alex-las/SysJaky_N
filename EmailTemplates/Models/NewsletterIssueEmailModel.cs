namespace SysJaky_N.EmailTemplates.Models;

public sealed class NewsletterIssueEmailModel
{
    public required string Subject { get; init; }

    public string? Preheader { get; init; }

    public string? IntroHtml { get; init; }

    public IReadOnlyList<NewsletterIssueEmailSectionModel> Sections { get; init; } = Array.Empty<NewsletterIssueEmailSectionModel>();

    public string? OutroHtml { get; init; }
}

public sealed record NewsletterIssueEmailSectionModel(string Title, string HtmlContent, string? CategoryName);
