using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Models;
using System.Threading;

namespace SysJaky_N.Data;

public static class CourseCategorySeeder
{
    private static readonly (string Name, string Slug)[] DefaultCategories = new (string Name, string Slug)[]
    {
        ("ISO 9001", "iso-9001"),
        ("ISO 14001", "iso-14001"),
        ("ISO/IEC 17025", "iso-iec-17025"),
        ("ISO 15189", "iso-15189"),
        ("ISO 45001", "iso-45001"),
        ("ISO 27001", "iso-27001"),
        ("ISO 13485", "iso-13485")
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var (name, slug) in DefaultCategories)
        {
            var exists = await context.CourseCategories
                .AsNoTracking()
                .AnyAsync(category => category.Slug == slug, cancellationToken);

            if (exists)
            {
                continue;
            }

            context.CourseCategories.Add(new CourseCategory
            {
                Name = name,
                Slug = slug
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
