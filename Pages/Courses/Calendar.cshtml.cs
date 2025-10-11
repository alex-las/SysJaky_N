using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Courses;

public class CalendarModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<CalendarModel> _localizer;

    public IStringLocalizer<CalendarModel> Localizer => _localizer;

    public CalendarModel(ApplicationDbContext context, IStringLocalizer<CalendarModel> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public DateTime CurrentMonth { get; set; }
    public List<List<DateTime?>> Weeks { get; set; } = new();
    public Dictionary<DateTime, List<Course>> CoursesByDate { get; set; } = new();

    public async Task OnGetAsync(int? year, int? month)
    {
        CurrentMonth = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);

        var firstDayOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
        var firstDay = firstDayOfMonth;
        while (firstDay.DayOfWeek != DayOfWeek.Monday)
        {
            firstDay = firstDay.AddDays(-1);
        }

        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        var lastDay = lastDayOfMonth;
        while (lastDay.DayOfWeek != DayOfWeek.Sunday)
        {
            lastDay = lastDay.AddDays(1);
        }

        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Monday)
            {
                Weeks.Add(new List<DateTime?>());
            }
            Weeks[^1].Add(date.Month == CurrentMonth.Month ? date : null);
        }

        var monthStart = firstDayOfMonth;
        var monthEnd = firstDayOfMonth.AddMonths(1);
        var courses = await _context.Courses
            .Where(c => c.Date >= monthStart && c.Date < monthEnd)
            .ToListAsync();

        CoursesByDate = courses
            .GroupBy(c => c.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}

