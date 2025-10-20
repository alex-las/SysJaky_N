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
    public DbSet<Company> Companies { get; set; } = default!;
    public DbSet<CompanyUser> CompanyUsers { get; set; } = default!;
    public DbSet<Enrollment> Enrollments { get; set; } = default!;
    public DbSet<SeatToken> SeatTokens { get; set; } = default!;
    public DbSet<LogEntry> LogEntries { get; set; } = default!;
    public DbSet<SalesStat> SalesStats { get; set; } = default!;
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; } = default!;
    public DbSet<PaymentId> PaymentIds { get; set; } = default!;
    public DbSet<PriceSchedule> PriceSchedules { get; set; } = default!;
    public DbSet<Instructor> Instructors { get; set; } = default!;
    public DbSet<Lesson> Lessons { get; set; } = default!;
    public DbSet<LessonProgress> LessonProgresses { get; set; } = default!;
    public DbSet<Testimonial> Testimonials { get; set; } = default!;
    public DbSet<ChatbotSettings> ChatbotSettings { get; set; } = default!;

    public DbSet<Certificate> Certificates { get; set; } = default!;
    public DbSet<Attendance> Attendances { get; set; } = default!;
    public DbSet<EmailLog> EmailLogs { get; set; } = default!;
    public DbSet<Tag> Tags { get; set; } = default!;
    public DbSet<CourseTag> CourseTags { get; set; } = default!;
    public DbSet<CourseCategory> CourseCategories { get; set; } = default!;
    public DbSet<CourseCategoryTranslation> CourseCategoryTranslations { get; set; } = default!;
    public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; } = default!;


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
        builder.Entity<Company>().HasIndex(c => c.ReferralCode).IsUnique();
        builder.Entity<Company>()
            .HasMany(c => c.Users)
            .WithOne(u => u.Company)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CompanyUser>()
            .HasIndex(cu => new { cu.CompanyId, cu.UserId })
            .IsUnique();
        builder.Entity<CompanyUser>()
            .HasOne(cu => cu.User)
            .WithMany(u => u.CompanyMemberships)
            .HasForeignKey(cu => cu.UserId)
            .OnDelete(DeleteBehavior.Cascade);
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
        builder.Entity<CourseTerm>()
            .HasOne(t => t.Instructor)
            .WithMany(i => i.CourseTerms)
            .HasForeignKey(t => t.InstructorId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.Entity<SeatToken>()
            .HasIndex(t => t.Token)
            .IsUnique();
        builder.Entity<SeatToken>()
            .HasOne(t => t.OrderItem)
            .WithMany(i => i.SeatTokens)
            .HasForeignKey(t => t.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SeatToken>()
            .HasOne(t => t.RedeemedByUser)
            .WithMany()
            .HasForeignKey(t => t.RedeemedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<PriceSchedule>()
            .HasOne(p => p.Course)
            .WithMany()
            .HasForeignKey(p => p.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PriceSchedule>()
            .HasIndex(p => new { p.CourseId, p.FromUtc, p.ToUtc });

        builder.Entity<Certificate>()
            .HasIndex(c => c.Number)
            .IsUnique();

        builder.Entity<Certificate>()
            .HasIndex(c => c.Hash)
            .IsUnique();

        builder.Entity<Certificate>()
            .HasOne(c => c.IssuedToEnrollment)
            .WithOne(e => e.Certificate)
            .HasForeignKey<Certificate>(c => c.IssuedToEnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Attendance>()
            .HasOne(a => a.Enrollment)
            .WithOne(e => e.Attendance)
            .HasForeignKey<Attendance>(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Attendance>()
            .HasIndex(a => a.EnrollmentId)
            .IsUnique();

        builder.Entity<EmailLog>()
            .HasIndex(e => e.SentUtc);

        builder.Entity<Lesson>()
            .HasOne(l => l.Course)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Lesson>()
            .HasIndex(l => new { l.CourseId, l.Order });

        builder.Entity<LessonProgress>()
            .HasKey(lp => new { lp.LessonId, lp.UserId });

        builder.Entity<LessonProgress>()
            .HasOne(lp => lp.Lesson)
            .WithMany(l => l.ProgressRecords)
            .HasForeignKey(lp => lp.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LessonProgress>()
            .HasOne(lp => lp.User)
            .WithMany(u => u.LessonProgresses)
            .HasForeignKey(lp => lp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        builder.Entity<NewsletterSubscriber>()
            .HasIndex(n => n.Email)
            .IsUnique();

        builder.Entity<NewsletterSubscriber>()
            .HasIndex(n => n.ConfirmationToken)
            .IsUnique();

        builder.Entity<CourseTag>()
            .HasKey(ct => new { ct.CourseId, ct.TagId });

        builder.Entity<CourseTag>()
            .HasOne(ct => ct.Course)
            .WithMany(c => c.CourseTags)
            .HasForeignKey(ct => ct.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CourseTag>()
            .HasOne(ct => ct.Tag)
            .WithMany(t => t.CourseTags)
            .HasForeignKey(ct => ct.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CourseCategory>()
            .ToTable("coursecategories");

        builder.Entity<CourseCategory>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        builder.Entity<CourseCategory>()
            .Property(c => c.SortOrder)
            .HasDefaultValue(0);

        builder.Entity<CourseCategory>()
            .Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.Entity<CourseCategory>()
            .HasMany(c => c.Translations)
            .WithOne(t => t.Category)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CourseCategoryTranslation>()
            .ToTable("coursecategory_translations");

        builder.Entity<CourseCategoryTranslation>()
            .Property(t => t.Locale)
            .HasMaxLength(10);

        builder.Entity<CourseCategoryTranslation>()
            .Property(t => t.Name)
            .HasMaxLength(100);

        builder.Entity<CourseCategoryTranslation>()
            .Property(t => t.Slug)
            .HasMaxLength(100);

        builder.Entity<CourseCategoryTranslation>()
            .Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Entity<CourseCategoryTranslation>()
            .HasIndex(t => new { t.CategoryId, t.Locale })
            .HasDatabaseName("uq_category_locale")
            .IsUnique();

        builder.Entity<CourseCategoryTranslation>()
            .HasIndex(t => new { t.Locale, t.Slug })
            .HasDatabaseName("uq_locale_slug")
            .IsUnique();

        builder.Entity<Course>()
            .HasMany(c => c.Categories)
            .WithMany(c => c.Courses)
            .UsingEntity<CourseCourseCategory>(
                j => j.HasOne(e => e.CourseCategory)
                    .WithMany(c => c.CourseCourseCategories)
                    .HasForeignKey(e => e.CourseCategoryId)
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne(e => e.Course)
                    .WithMany(c => c.CourseCourseCategories)
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey(e => new { e.CourseId, e.CourseCategoryId });
                    j.ToTable("course_coursecategories");
                    j.HasIndex(e => e.CourseCategoryId);
                });

        builder.Entity<ChatbotSettings>()
            .Property(s => s.IsEnabled)
            .HasDefaultValue(true);

        builder.Entity<ChatbotSettings>()
            .Property(s => s.AutoInitialize)
            .HasDefaultValue(true);
    }
}
