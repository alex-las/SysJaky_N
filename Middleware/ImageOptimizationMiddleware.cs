using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SysJaky_N.Middleware;

public class ImageOptimizationMiddleware
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageOptimizationMiddleware> _logger;

    public ImageOptimizationMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<ImageOptimizationMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Path.HasValue)
        {
            await _next(context);
            return;
        }

        var relativePath = context.Request.Path.Value!;
        var extension = Path.GetExtension(relativePath);
        if (string.IsNullOrEmpty(extension) || !SupportedExtensions.Contains(extension))
        {
            await _next(context);
            return;
        }

        var physicalSourcePath = Path.Combine(
            _environment.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(physicalSourcePath))
        {
            await _next(context);
            return;
        }

        var width = TryParseWidth(context.Request.Query["w"]);
        var requestedFormat = context.Request.Query.TryGetValue("format", out var formatValues)
            ? formatValues.ToString()?.ToLowerInvariant()
            : null;

        var acceptsWebp = context.Request.Headers.TryGetValue(HeaderNames.Accept, out var accepts)
            && accepts.Any(value => value.Contains("image/webp", StringComparison.OrdinalIgnoreCase));

        var targetFormat = DetermineTargetFormat(extension, requestedFormat, acceptsWebp);
        if (targetFormat is null)
        {
            await _next(context);
            return;
        }

        var cachePath = GetCacheFilePath(relativePath, width, targetFormat.Value);
        var semaphore = FileLocks.GetOrAdd(cachePath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            var shouldRegenerate = !File.Exists(cachePath)
                || File.GetLastWriteTimeUtc(cachePath) < File.GetLastWriteTimeUtc(physicalSourcePath);

            if (shouldRegenerate)
            {
                await CreateOptimizedImageAsync(physicalSourcePath, cachePath, width, targetFormat.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image optimization failed for {Path}", relativePath);
            await _next(context);
            return;
        }
        finally
        {
            semaphore.Release();
        }

        context.Response.ContentType = targetFormat.Value == TargetImageFormat.Webp
            ? "image/webp"
            : "image/jpeg";
        context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=31536000,immutable";
        await context.Response.SendFileAsync(cachePath);
    }

    private static int? TryParseWidth(StringValues widthValues)
    {
        if (widthValues.Count == 0)
        {
            return null;
        }

        if (int.TryParse(widthValues.ToString(), out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    private TargetImageFormat? DetermineTargetFormat(string originalExtension, string? requestedFormat, bool acceptsWebp)
    {
        if (string.Equals(requestedFormat, "webp", StringComparison.OrdinalIgnoreCase)
            || (requestedFormat is null && acceptsWebp))
        {
            return TargetImageFormat.Webp;
        }

        if (string.Equals(requestedFormat, "jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestedFormat, "jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return TargetImageFormat.Jpeg;
        }

        if (requestedFormat is null)
        {
            if (acceptsWebp)
            {
                return TargetImageFormat.Webp;
            }

            if (string.Equals(originalExtension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(originalExtension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return TargetImageFormat.Jpeg;
            }
        }

        return null;
    }

    private string GetCacheFilePath(string relativePath, int? width, TargetImageFormat format)
    {
        var cacheDirectory = Path.Combine(_environment.WebRootPath, "optimized-images");
        Directory.CreateDirectory(cacheDirectory);

        var builder = new StringBuilder(relativePath.TrimStart('/').Replace('/', '_'));
        if (width.HasValue)
        {
            builder.Append($"_{width.Value}");
        }

        var extension = format == TargetImageFormat.Webp ? ".webp" : ".jpg";
        builder.Append(extension);

        return Path.Combine(cacheDirectory, builder.ToString());
    }

    private static async Task CreateOptimizedImageAsync(string sourcePath, string destinationPath, int? width, TargetImageFormat format)
    {
        await using var output = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var image = await Image.LoadAsync(sourcePath);

        if (width.HasValue && image.Width > width.Value)
        {
            image.Mutate(options =>
                options.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(width.Value, 0)
                }));
        }

        switch (format)
        {
            case TargetImageFormat.Webp:
                var webpEncoder = new WebpEncoder
                {
                    Quality = 75
                };
                await image.SaveAsync(output, webpEncoder);
                break;
            case TargetImageFormat.Jpeg:
                var jpegEncoder = new JpegEncoder
                {
                    Quality = 82,
                    Interleaved = true
                };
                await image.SaveAsync(output, jpegEncoder);
                break;
        }
    }

    private enum TargetImageFormat
    {
        Webp,
        Jpeg
    }
}
