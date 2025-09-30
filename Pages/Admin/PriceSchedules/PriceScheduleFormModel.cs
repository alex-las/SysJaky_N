using Microsoft.AspNetCore.Mvc.Rendering;
using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.PriceSchedules;

public class PriceScheduleFormModel
{
    public PriceScheduleFormModel(PriceSchedule priceSchedule, IEnumerable<SelectListItem> courses)
    {
        PriceSchedule = priceSchedule;
        Courses = courses;
    }

    public PriceSchedule PriceSchedule { get; }

    public IEnumerable<SelectListItem> Courses { get; }
}
