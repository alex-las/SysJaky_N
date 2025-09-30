using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysJaky_N.Pages.Admin.CourseTerms;

public class CourseTermFormModel
{
    public CourseTermFormModel(CourseTermInputModel input, IEnumerable<SelectListItem> courseOptions, IEnumerable<SelectListItem> instructorOptions)
    {
        Input = input;
        CourseOptions = courseOptions;
        InstructorOptions = instructorOptions;
    }

    public CourseTermInputModel Input { get; }

    public IEnumerable<SelectListItem> CourseOptions { get; }

    public IEnumerable<SelectListItem> InstructorOptions { get; }
}
