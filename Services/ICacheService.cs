using System.Threading;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface ICacheService
{
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<Course?> GetCourseAsync(int courseId, CancellationToken cancellationToken = default);

    void RemoveCourseList();

    void RemoveCourseDetail(int courseId);
}
