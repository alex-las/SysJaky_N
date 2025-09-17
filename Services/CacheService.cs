using System;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CacheService : ICacheService
{
    private const string CoursesCacheKey = "courses:list";
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public CacheService(ApplicationDbContext context, IMemoryCache memoryCache)
    {
        _context = context;
        _memoryCache = memoryCache;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromSeconds(60));
    }

    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CoursesCacheKey, out IReadOnlyList<Course> cachedCourses))
        {
            return cachedCourses;
        }

        var courses = await _context.Courses
            .AsNoTracking()
            .Include(c => c.CourseGroup)
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var readOnly = courses.AsReadOnly();
        _memoryCache.Set(CoursesCacheKey, readOnly, _cacheOptions);
        return readOnly;
    }

    public async Task<Course?> GetCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCourseDetailKey(courseId);
        if (_memoryCache.TryGetValue(cacheKey, out Course cachedCourse))
        {
            return cachedCourse;
        }

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.CourseGroup)
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course != null)
        {
            _memoryCache.Set(cacheKey, course, _cacheOptions);
        }

        return course;
    }

    public void RemoveCourseList()
    {
        _memoryCache.Remove(CoursesCacheKey);
    }

    public void RemoveCourseDetail(int courseId)
    {
        _memoryCache.Remove(GetCourseDetailKey(courseId));
    }

    private static string GetCourseDetailKey(int courseId) => $"course:detail:{courseId}";
}
