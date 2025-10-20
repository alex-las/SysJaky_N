using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    protected IStringLocalizer<IndexModel> Localizer { get; }

    public enum PriceScheduleStatus
    {
        Active,
        Upcoming,
        Expired
    }

    public class PriceScheduleItemViewModel
    {
        private readonly HashSet<int> _conflictIds = new();

        public PriceSchedule Schedule { get; init; } = default!;

        public PriceScheduleStatus Status { get; init; }

        public List<PriceSchedule> Conflicts { get; } = new();

        public bool HasConflicts => Conflicts.Count > 0;

        public string StatusDisplay => Status switch
        {
            PriceScheduleStatus.Active => "Aktivní",
            PriceScheduleStatus.Upcoming => "Plánovaný",
            PriceScheduleStatus.Expired => "Expirovaný",
            _ => Status.ToString()
        };

        public string StatusBadgeCssClass => Status switch
        {
            PriceScheduleStatus.Active => "bg-success",
            PriceScheduleStatus.Upcoming => "bg-info text-dark",
            PriceScheduleStatus.Expired => "bg-secondary",
            _ => "bg-light text-dark"
        };

        public void AddConflict(PriceSchedule conflictingSchedule)
        {
            if (_conflictIds.Add(conflictingSchedule.Id))
            {
                Conflicts.Add(conflictingSchedule);
            }
        }
    }

    public class CoursePriceSchedulesViewModel
    {
        public Course? Course { get; init; }

        public List<PriceScheduleItemViewModel> Schedules { get; init; } = new();
    }

    public class PriceScheduleSummaryViewModel
    {
        public int Total { get; init; }

        public int Active { get; init; }

        public int Upcoming { get; init; }

        public int Expired { get; init; }
    }

    public IndexModel(ApplicationDbContext context, IStringLocalizer<IndexModel> localizer)
    {
        _context = context;
        Localizer = localizer;
    }

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public PriceScheduleStatus? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool FutureOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool HideExpired { get; set; }

    public IList<CoursePriceSchedulesViewModel> Groups { get; private set; } = new List<CoursePriceSchedulesViewModel>();

    public List<SelectListItem> CourseOptions { get; private set; } = new();

    public List<SelectListItem> StatusOptions { get; private set; } = new();

    public PriceScheduleSummaryViewModel Summary { get; private set; } = new();

    public bool HasConflicts { get; private set; }

    public async Task OnGetAsync()
    {
        _ = Localizer["IndexPageName"];

        await LoadFilterOptionsAsync();

        var now = DateTime.UtcNow;

        var query = _context.PriceSchedules
            .AsNoTracking()
            .Include(p => p.Course)
            .AsQueryable();

        if (CourseId.HasValue)
        {
            query = query.Where(p => p.CourseId == CourseId.Value);
        }

        if (FutureOnly)
        {
            query = query.Where(p => p.FromUtc >= now);
        }

        if (HideExpired)
        {
            query = query.Where(p => p.ToUtc > now);
        }

        if (StatusFilter.HasValue)
        {
            query = StatusFilter.Value switch
            {
                PriceScheduleStatus.Active => query.Where(p => p.FromUtc <= now && now < p.ToUtc),
                PriceScheduleStatus.Upcoming => query.Where(p => p.FromUtc > now),
                PriceScheduleStatus.Expired => query.Where(p => p.ToUtc <= now),
                _ => query
            };
        }

        var priceSchedules = await query
            .OrderBy(p => p.Course!.Title)
            .ThenBy(p => p.FromUtc)
            .ToListAsync();

        var grouped = priceSchedules
            .GroupBy(p => p.CourseId)
            .Select(group => new CoursePriceSchedulesViewModel
            {
                Course = group.First().Course,
                Schedules = group
                    .Select(schedule => new PriceScheduleItemViewModel
                    {
                        Schedule = schedule,
                        Status = DetermineStatus(schedule, now)
                    })
                    .OrderBy(s => s.Schedule.FromUtc)
                    .ToList()
            })
            .OrderBy(g => g.Course?.Title)
            .ToList();

        foreach (var group in grouped)
        {
            for (var i = 0; i < group.Schedules.Count; i++)
            {
                for (var j = i + 1; j < group.Schedules.Count; j++)
                {
                    if (HasOverlap(group.Schedules[i].Schedule, group.Schedules[j].Schedule))
                    {
                        group.Schedules[i].AddConflict(group.Schedules[j].Schedule);
                        group.Schedules[j].AddConflict(group.Schedules[i].Schedule);
                    }
                }
            }
        }

        Groups = grouped;
        HasConflicts = Groups.Any(g => g.Schedules.Any(s => s.HasConflicts));

        var flatSchedules = Groups.SelectMany(g => g.Schedules).ToList();
        Summary = new PriceScheduleSummaryViewModel
        {
            Total = flatSchedules.Count,
            Active = flatSchedules.Count(s => s.Status == PriceScheduleStatus.Active),
            Upcoming = flatSchedules.Count(s => s.Status == PriceScheduleStatus.Upcoming),
            Expired = flatSchedules.Count(s => s.Status == PriceScheduleStatus.Expired)
        };
    }

    private async Task LoadFilterOptionsAsync()
    {
        CourseOptions = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), CourseId.HasValue && CourseId.Value == c.Id))
            .ToListAsync();

        CourseOptions.Insert(0, new SelectListItem("Všechny kurzy", string.Empty, !CourseId.HasValue));

        StatusOptions = Enum.GetValues(typeof(PriceScheduleStatus))
            .Cast<PriceScheduleStatus>()
            .Select(status => new SelectListItem(GetStatusLabel(status), status.ToString(), StatusFilter == status))
            .ToList();

        StatusOptions.Insert(0, new SelectListItem("Všechny stavy", string.Empty, !StatusFilter.HasValue));
    }

    private static PriceScheduleStatus DetermineStatus(PriceSchedule schedule, DateTime now)
    {
        if (schedule.FromUtc <= now && now < schedule.ToUtc)
        {
            return PriceScheduleStatus.Active;
        }

        if (schedule.FromUtc > now)
        {
            return PriceScheduleStatus.Upcoming;
        }

        return PriceScheduleStatus.Expired;
    }

    private static bool HasOverlap(PriceSchedule left, PriceSchedule right)
    {
        return left.FromUtc < right.ToUtc && right.FromUtc < left.ToUtc;
    }

    private static string GetStatusLabel(PriceScheduleStatus status) => status switch
    {
        PriceScheduleStatus.Active => "Aktivní",
        PriceScheduleStatus.Upcoming => "Plánovaný",
        PriceScheduleStatus.Expired => "Expirovaný",
        _ => status.ToString()
    };
}
