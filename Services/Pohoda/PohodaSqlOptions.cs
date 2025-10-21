namespace SysJaky_N.Services.Pohoda;

public interface IPohodaSqlOptions
{
    string ConnectionString { get; }
    string? Server { get; }
    string? Database { get; }
    string? Username { get; }
    string? Password { get; }
    TimeSpan ExportWorkerInterval { get; }
    int ExportWorkerBatchSize { get; }
    int MaxRetryAttempts { get; }
    TimeSpan RetryBaseDelay { get; }
    TimeSpan RetryMaxDelay { get; }
}

public class PohodaSqlOptions : IPohodaSqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string? Server { get; set; }

    public string? Database { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public TimeSpan ExportWorkerInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int ExportWorkerBatchSize { get; set; } = 10;

    public int MaxRetryAttempts { get; set; } = 5;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(10);
}
