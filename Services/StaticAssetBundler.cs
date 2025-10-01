using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Services;

internal static class StaticAssetBundler
{
    private static readonly Regex CssCommentRegex = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex JsSingleLineCommentRegex = new(@"//.*?(?=\r?\n)", RegexOptions.Compiled);
    private static readonly Regex JsMultiLineCommentRegex = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CssWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex JsWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex CssSpacingRegex = new(@"\s*([{};:,])\s*", RegexOptions.Compiled);
    private static readonly Regex JsSpacingRegex = new(@"\s*([=+\-*/%<>!?&|^,;:{}()\[\]])\s*", RegexOptions.Compiled);

    public static void Build(IWebHostEnvironment environment, ILogger logger)
    {
        try
        {
            var distPath = Path.Combine(environment.WebRootPath, "dist");
            Directory.CreateDirectory(distPath);

            var cssSources = new[]
            {
                Path.Combine("lib", "bootstrap", "dist", "css", "bootstrap.min.css"),
                Path.Combine("css", "site.css")
            };

            var jsSources = new[]
            {
                Path.Combine("lib", "jquery", "dist", "jquery.min.js"),
                Path.Combine("lib", "bootstrap", "dist", "js", "bootstrap.bundle.min.js"),
                Path.Combine("js", "site.js")
            };

            BuildBundle(environment, cssSources, Path.Combine(distPath, "styles.min.css"), isCss: true, logger);
            BuildBundle(environment, jsSources, Path.Combine(distPath, "scripts.min.js"), isCss: false, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asset bundling failed");
        }
    }

    private static void BuildBundle(
        IWebHostEnvironment environment,
        IEnumerable<string> sources,
        string destination,
        bool isCss,
        ILogger logger)
    {
        var builder = new StringBuilder();

        foreach (var source in sources)
        {
            var physicalPath = Path.Combine(environment.WebRootPath, source);
            if (!File.Exists(physicalPath))
            {
                logger.LogWarning("Bundle source not found: {Path}", physicalPath);
                continue;
            }

            var content = File.ReadAllText(physicalPath);
            if (Path.GetFileNameWithoutExtension(source).EndsWith(".min", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine(content);
            }
            else
            {
                builder.AppendLine(isCss ? MinifyCss(content) : MinifyJs(content));
            }
        }

        File.WriteAllText(destination, builder.ToString());
    }

    private static string MinifyCss(string content)
    {
        var withoutComments = CssCommentRegex.Replace(content, string.Empty);
        var collapsedWhitespace = CssWhitespaceRegex.Replace(withoutComments, " ");
        var tightened = CssSpacingRegex.Replace(collapsedWhitespace, "$1");
        return tightened.Replace(";}", "}").Trim();
    }

    private static string MinifyJs(string content)
    {
        var withoutSingleLine = JsSingleLineCommentRegex.Replace(content, string.Empty);
        var withoutComments = JsMultiLineCommentRegex.Replace(withoutSingleLine, string.Empty);
        var collapsedWhitespace = JsWhitespaceRegex.Replace(withoutComments, " ");
        var tightened = JsSpacingRegex.Replace(collapsedWhitespace, "$1");
        return tightened.Trim();
    }
}
