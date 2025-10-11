using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Services;

public class LocalCourseMediaStorage : ICourseMediaStorage
{
    private readonly IStringLocalizer<LocalCourseMediaStorage> _localizer;
    private readonly string _webRootPath;

    public LocalCourseMediaStorage(
        IWebHostEnvironment environment,
        IStringLocalizer<LocalCourseMediaStorage> localizer)
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _webRootPath = environment.WebRootPath
                       ?? throw new InvalidOperationException(_localizer["Error.WebRootMissing"].Value);
    }

    public async Task<string> SaveCoverImageAsync(int courseId, Stream imageStream, string contentType, CancellationToken cancellationToken = default)
    {
        if (imageStream == null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        if (!string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(_localizer["Error.InvalidFormat"].Value);
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
