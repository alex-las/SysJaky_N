namespace SysJaky_N.Services.Pohoda;

public interface IPohodaSqlOptions
{
    string ConnectionString { get; }
    string? Server { get; }
    string? Database { get; }
    string? Username { get; }
    string? Password { get; }
}

public class PohodaSqlOptions : IPohodaSqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string? Server { get; set; }

    public string? Database { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
