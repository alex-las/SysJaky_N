using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlOptions
{
    public const string SectionName = "PohodaXml";

    public bool Enabled { get; set; } = true;

    public string ExportDirectory { get; set; } = "temp";

    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Application { get; set; } = string.Empty;

    public string Instance { get; set; } = string.Empty;

    public string Company { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EncodingName { get; set; } = "windows-1250";

    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 100;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    public bool CheckDuplicity { get; set; } = true;

    public TimeSpan ExportWorkerInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int ExportWorkerBatchSize { get; set; } = 10;

    public int MaxRetryAttempts { get; set; } = 5;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(10);
}
