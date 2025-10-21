using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Services.Pohoda;

public sealed class PohodaXmlOptions
{
    public const string SectionName = "PohodaXml";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string Application { get; set; } = string.Empty;

    public string Instance { get; set; } = string.Empty;

    public bool CheckDuplicity { get; set; } = true;

    public TimeSpan ExportWorkerInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int ExportWorkerBatchSize { get; set; } = 10;

    public int MaxRetryAttempts { get; set; } = 5;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(10);
}
