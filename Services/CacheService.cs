using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace SysJaky_N.Services;

public class CacheService : ICacheService
{
    private const string CourseListKeyPrefix = "CourseList:";
    private const string CourseDetailKeyPrefix = "CourseDetail:";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, byte> _courseListKeys = new();

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<CourseListCacheEntry> GetCourseListAsync(string cacheKey, Func<Task<CourseListCacheEntry>> factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var fullKey = CourseListKeyPrefix + cacheKey;
        var result = await _memoryCache.GetOrCreateAsync(fullKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            return factory();
        });

        if (result == null)
        {
            throw new InvalidOperationException("Course list factory returned null.");
        }

        _courseListKeys.TryAdd(fullKey, 0);
        return result;
    }

    public Task<CourseDetailCacheEntry?> GetCourseDetailAsync(
        int courseId,
        Func<Task<CourseDetailCacheEntry?>> factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var key = GetCourseDetailKey(courseId);
        return _memoryCache.GetOrCreateAsync(key, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            return factory();
        });
    }

    public void InvalidateCourseList()
    {
        var keys = _courseListKeys.Keys.ToList();
        foreach (var key in keys)
        {
            _memoryCache.Remove(key);
            _courseListKeys.TryRemove(key, out _);
        }
    }

    public void InvalidateCourseDetail(int courseId)
    {
        var key = GetCourseDetailKey(courseId);
        _memoryCache.Remove(key);
    }

    private static string GetCourseDetailKey(int courseId) => CourseDetailKeyPrefix + courseId;
}
