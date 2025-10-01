using System;
using System.Text;

namespace SysJaky_N.Extensions;

public static class ImageUrlExtensions
{
    public static string WithImageParameters(this string? url, int? width = null, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (IsExternalUrl(url) || (width is null && string.IsNullOrWhiteSpace(format)))
        {
            return url;
        }

        var builder = new StringBuilder(url);
        var hasQuery = url.Contains('?', StringComparison.Ordinal);
        var appended = false;

        if (width.HasValue && !ContainsQueryKey(url, "w"))
        {
            builder.Append(hasQuery ? '&' : '?');
            builder.Append("w=");
            builder.Append(width.Value);
            hasQuery = true;
            appended = true;
        }

        if (!string.IsNullOrWhiteSpace(format) && !ContainsQueryKey(url, "format"))
        {
            builder.Append(hasQuery ? '&' : '?');
            builder.Append("format=");
            builder.Append(format);
            appended = true;
        }

        return appended ? builder.ToString() : url;
    }

    private static bool ContainsQueryKey(string url, string key)
    {
        return url.Contains($"?{key}=", StringComparison.OrdinalIgnoreCase)
               || url.Contains($"&{key}=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExternalUrl(string url)
    {
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var absolute)
               && absolute.Scheme is "http" or "https";
    }
}
