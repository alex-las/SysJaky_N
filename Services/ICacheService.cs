using System.Collections.Generic;
using System.Threading.Tasks;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public record CourseListCacheEntry(
    IReadOnlyList<Course> Courses,
    IReadOnlyDictionary<int, CourseTermSnapshot> TermSnapshots,
    int TotalPages,
    int TotalCount);

public record CourseDetailCacheEntry(
    Course Course,
    CourseBlock? CourseBlock,
    IReadOnlyList<CourseReview> Reviews,
    IReadOnlyList<Lesson> Lessons,
    IReadOnlyList<CourseTerm> Terms);

public interface ICacheService
{
    Task<CourseListCacheEntry> GetCourseListAsync(string cacheKey, Func<Task<CourseListCacheEntry>> factory);

    Task<CourseDetailCacheEntry?> GetCourseDetailAsync(int courseId, Func<Task<CourseDetailCacheEntry?>> factory);

    void InvalidateCourseList();

    void InvalidateCourseDetail(int courseId);
}
