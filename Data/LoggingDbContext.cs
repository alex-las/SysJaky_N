using Microsoft.EntityFrameworkCore;
using SysJaky_N.Models;

namespace SysJaky_N.Data;

internal sealed class LoggingDbContext : DbContext   // ← bylo: public class
{
    public LoggingDbContext(DbContextOptions<LoggingDbContext> options) : base(options) { }
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(b =>
        {
            b.ToTable("LogEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Level).HasMaxLength(32);
            b.Property(x => x.SourceContext).HasMaxLength(256);
        });
    }
}
