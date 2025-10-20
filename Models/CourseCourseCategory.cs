namespace SysJaky_N.Models;

public class CourseCourseCategory
{
    public int CourseId { get; set; }

    public Course Course { get; set; } = default!;

    public int CourseCategoryId { get; set; }

    public CourseCategory CourseCategory { get; set; } = default!;
}
