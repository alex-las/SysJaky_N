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
    public DbSet<Order> Orders { get; set; } = default!;
    public DbSet<OrderItem> OrderItems { get; set; } = default!;
    public DbSet<DiscountCode> DiscountCodes { get; set; } = default!;
    public DbSet<Article> Articles { get; set; } = default!;
    public DbSet<CourseReview> CourseReviews { get; set; } = default!;
    public DbSet<AuditLog> AuditLogs { get; set; } = default!;
    public DbSet<ContactMessage> ContactMessages { get; set; } = default!;
    public DbSet<CourseBlock> CourseBlocks { get; set; } = default!;

}
