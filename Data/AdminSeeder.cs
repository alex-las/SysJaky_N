using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Models;

namespace SysJaky_N.Data;

public class AdminSeeder
{
    private readonly IServiceProvider _serviceProvider;

    public AdminSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task SeedAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        var role = configuration["SeedAdmin:Role"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        if (await context.ApplicationUsers.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var user = new ApplicationUser
        {
            Email = email,
            Password = password,
            Role = role
        };

        context.ApplicationUsers.Add(user);
        await context.SaveChangesAsync();
    }
}

