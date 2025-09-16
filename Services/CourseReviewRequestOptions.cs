namespace SysJaky_N.Services;

public class CourseReviewRequestOptions
{
    public string PublicBaseUrl { get; set; } = "https://localhost";

    public string FormPathTemplate { get; set; } = "/Courses/Details/{courseId}";

    public int CheckIntervalHours { get; set; } = 24;
}
