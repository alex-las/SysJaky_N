namespace SysJaky_N.EmailTemplates.Models;

public sealed class NewsletterIssueEmailModel
{
    public required string Subject { get; init; }

    public string? Preheader { get; init; }

    public string? IntroHtml { get; init; }

    public string? OutroHtml { get; init; }

    public required NewsletterTemplateEmailModel Template { get; init; }

    public IReadOnlyList<NewsletterIssueEmailRegionModel> Regions { get; init; } = Array.Empty<NewsletterIssueEmailRegionModel>();
}

public sealed class NewsletterTemplateEmailModel
{
    public required string Name { get; init; }

    public string PrimaryColor { get; init; } = "#2563eb";

    public string SecondaryColor { get; init; } = "#facc15";

    public string BackgroundColor { get; init; } = "#f9fafb";

    public string BaseLayoutHtml { get; init; } = string.Empty;
}

public sealed class NewsletterIssueEmailRegionModel
{
    public required string Name { get; init; }

    public string? CategoryName { get; init; }

    public IReadOnlyList<NewsletterIssueEmailSectionModel> Sections { get; init; } = Array.Empty<NewsletterIssueEmailSectionModel>();
}

public sealed record NewsletterIssueEmailSectionModel(string Title, string HtmlContent);
