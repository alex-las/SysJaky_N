using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Models;

namespace SysJaky_N.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Course> Courses { get; set; } = default!;
    public DbSet<CourseGroup> CourseGroups { get; set; } = default!;
    public DbSet<CourseTerm> CourseTerms { get; set; } = default!;
    public DbSet<Order> Orders { get; set; } = default!;
    public DbSet<OrderItem> OrderItems { get; set; } = default!;
    public DbSet<Voucher> Vouchers { get; set; } = default!;
    public DbSet<Article> Articles { get; set; } = default!;
    public DbSet<CourseReview> CourseReviews { get; set; } = default!;
    public DbSet<AuditLog> AuditLogs { get; set; } = default!;
    public DbSet<ContactMessage> ContactMessages { get; set; } = default!;
    public DbSet<CourseBlock> CourseBlocks { get; set; } = default!;
    public DbSet<WishlistItem> WishlistItems { get; set; } = default!;
    public DbSet<CompanyProfile> CompanyProfiles { get; set; } = default!;
    public DbSet<Enrollment> Enrollments { get; set; } = default!;
    public DbSet<LogEntry> LogEntries { get; set; } = default!;
    public DbSet<SalesStat> SalesStats { get; set; } = default!;
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; } = default!;
    public DbSet<PaymentId> PaymentIds { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Course>().HasIndex(c => c.IsActive);
        builder.Entity<CourseTerm>().HasIndex(ct => new { ct.CourseId, ct.StartUtc });
        builder.Entity<WishlistItem>().HasKey(w => new { w.UserId, w.CourseId });
        builder.Entity<CompanyProfile>().HasIndex(c => c.ReferenceCode).IsUnique();
        builder.Entity<CompanyProfile>()
            .HasMany(c => c.Users)
            .WithOne(u => u.CompanyProfile)
            .HasForeignKey(u => u.CompanyProfileId);
        builder.Entity<CompanyProfile>()
            .HasOne(c => c.Manager)
            .WithMany()
            .HasForeignKey(c => c.ManagerId);
        builder.Entity<Voucher>()
            .HasIndex(v => v.Code)
            .IsUnique();
        builder.Entity<Voucher>()
            .HasOne(v => v.AppliesToCourse)
            .WithMany()
            .HasForeignKey(v => v.AppliesToCourseId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<CourseTerm>()
            .HasOne(t => t.Course)
            .WithMany()
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Enrollment>()
            .HasOne(e => e.User)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Enrollment>()
            .HasOne(e => e.CourseTerm)
            .WithMany(t => t.Enrollments)
            .HasForeignKey(e => e.CourseTermId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<LogEntry>().HasIndex(e => e.Timestamp);
        builder.Entity<SalesStat>().HasKey(s => s.Date);

        builder.Entity<WaitlistEntry>()
            .HasIndex(w => new { w.UserId, w.CourseTermId })
            .IsUnique();
        builder.Entity<WaitlistEntry>()
            .HasIndex(w => new { w.CourseTermId, w.CreatedAtUtc });
        builder.Entity<WaitlistEntry>()
            .HasOne(w => w.User)
            .WithMany(u => u.WaitlistEntries)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WaitlistEntry>()
            .HasOne(w => w.CourseTerm)
            .WithMany(t => t.WaitlistEntries)
            .HasForeignKey(w => w.CourseTermId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PaymentId>().HasKey(p => p.Id);
    }
}
