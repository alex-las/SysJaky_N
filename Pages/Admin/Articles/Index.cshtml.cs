using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.Articles;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<IndexModel> _localizer;

    public IList<Article> Articles { get; set; } = new List<Article>();

    [BindProperty(SupportsGet = true)]
    public string? SearchTitle { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedAuthorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedTo { get; set; }

    public IEnumerable<SelectListItem> AuthorOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> PublicationOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    [BindProperty(SupportsGet = true)]
    public PublicationFilterOption? PublicationFilter { get; set; }

    public DateTime NowUtc { get; private set; }

    public enum PublicationFilterOption
    {
        Draft,
        Published,
        Scheduled
    }

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = _localizer["Title"];

        if (!string.IsNullOrWhiteSpace(SearchTitle))
        {
            SearchTitle = SearchTitle.Trim();
        }

        if (CreatedFrom.HasValue)
        {
            CreatedFrom = CreatedFrom.Value.Date;
        }

        if (CreatedTo.HasValue)
        {
            CreatedTo = CreatedTo.Value.Date;
        }

        AuthorOptions = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = string.IsNullOrWhiteSpace(u.UserName) ? u.Email ?? u.Id : u.UserName,
                Selected = u.Id == SelectedAuthorId
            })
            .ToListAsync();

        PublicationOptions = new List<SelectListItem>
        {
            new()
            {
                Value = string.Empty,
                Text = _localizer["FilterPublicationAllOption"],
                Selected = PublicationFilter == null
            },
            new()
            {
                Value = PublicationFilterOption.Published.ToString(),
                Text = _localizer["FilterPublicationPublishedOption"],
                Selected = PublicationFilter == PublicationFilterOption.Published
            },
            new()
            {
                Value = PublicationFilterOption.Draft.ToString(),
                Text = _localizer["FilterPublicationDraftOption"],
                Selected = PublicationFilter == PublicationFilterOption.Draft
            },
            new()
            {
                Value = PublicationFilterOption.Scheduled.ToString(),
                Text = _localizer["FilterPublicationScheduledOption"],
                Selected = PublicationFilter == PublicationFilterOption.Scheduled
            }
        };

        NowUtc = DateTime.UtcNow;
        var now = NowUtc;

        var query = _context.Articles
            .Include(a => a.Author)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchTitle))
        {
            var pattern = $"%{SearchTitle}%";
            query = query.Where(a => EF.Functions.Like(a.Title, pattern));
        }

        if (!string.IsNullOrWhiteSpace(SelectedAuthorId))
        {
            query = query.Where(a => a.AuthorId == SelectedAuthorId);
        }

        if (CreatedFrom.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= CreatedFrom.Value);
        }

        if (CreatedTo.HasValue)
        {
            var toExclusive = CreatedTo.Value.AddDays(1);
            query = query.Where(a => a.CreatedAt < toExclusive);
        }

        if (PublicationFilter == PublicationFilterOption.Published)
        {
            query = query.Where(a => a.IsPublished && a.PublishedAtUtc <= now);
        }
        else if (PublicationFilter == PublicationFilterOption.Draft)
        {
            query = query.Where(a => !a.IsPublished);
        }
        else if (PublicationFilter == PublicationFilterOption.Scheduled)
        {
            query = query.Where(a => a.IsPublished && (!a.PublishedAtUtc.HasValue || a.PublishedAtUtc > now));
        }

        Articles = await query
            .OrderByDescending(a => a.UpdatedAtUtc)
            .AsNoTracking()
            .ToListAsync();
    }
}
