using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlOptionsValidator : IValidateOptions<PohodaXmlOptions>
{
    public ValidateOptionsResult Validate(string? name, PohodaXmlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                failures.Add("PohodaXml:BaseUrl is required when the Pohoda integration is enabled.");
            }

            if (string.IsNullOrWhiteSpace(options.Username))
            {
                failures.Add("PohodaXml:Username is required when the Pohoda integration is enabled.");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                failures.Add("PohodaXml:Password is required when the Pohoda integration is enabled.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ExportDirectory))
        {
            try
            {
                _ = Path.GetFullPath(options.ExportDirectory);
            }
            catch (Exception ex)
            {
                failures.Add($"PohodaXml:ExportDirectory is invalid: {ex.Message}");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
