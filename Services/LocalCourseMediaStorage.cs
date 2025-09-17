using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace SysJaky_N.Services;

public class LocalCourseMediaStorage : ICourseMediaStorage
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _webRootPath;

    public LocalCourseMediaStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
        _webRootPath = environment.WebRootPath ?? throw new InvalidOperationException("Web root path is not configured.");
    }

    public async Task<string> SaveCoverImageAsync(int courseId, Stream imageStream, string contentType, CancellationToken cancellationToken = default)
    {
        if (imageStream == null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        if (!string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only JPEG cover images are supported for local storage.");
        }

        var courseDirectory = Path.Combine(_webRootPath, "media", "courses", courseId.ToString());
        Directory.CreateDirectory(courseDirectory);
        var filePath = Path.Combine(courseDirectory, "cover.jpg");

        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await imageStream.CopyToAsync(fileStream, cancellationToken);

        return $"/media/courses/{courseId}/cover.jpg";
    }

    public Task DeleteCoverImageAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_webRootPath, "media", "courses", courseId.ToString(), "cover.jpg");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
