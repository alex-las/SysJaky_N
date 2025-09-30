using SysJaky_N.Models;

namespace SysJaky_N.Pages.Admin.CourseBlocks;

public class CourseBlockFormModel
{
    public CourseBlockFormModel(CourseBlock courseBlock, IList<Course> availableCourses, ICollection<int> selectedCourseIds)
    {
        CourseBlock = courseBlock;
        AvailableCourses = availableCourses;
        SelectedCourseIds = selectedCourseIds;
    }

    public CourseBlock CourseBlock { get; }

    public IList<Course> AvailableCourses { get; }

    public ICollection<int> SelectedCourseIds { get; }
}
