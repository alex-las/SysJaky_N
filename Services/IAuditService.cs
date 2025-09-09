using System.Threading.Tasks;

namespace SysJaky_N.Services;

public interface IAuditService
{
    Task LogAsync(string? userId, string action, string? data = null);
}

