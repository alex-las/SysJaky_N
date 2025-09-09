using System;
using System.Threading.Tasks;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string? userId, string action, string? data = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Timestamp = DateTime.UtcNow,
            Data = data
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}

