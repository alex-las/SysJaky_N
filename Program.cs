using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddRazorPages();
builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    var seeder = new AdminSeeder(services);
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapPost("/payment/webhook", async (HttpRequest request, PaymentService paymentService) =>
{
    await paymentService.HandleWebhookAsync(request);
    return Results.Ok();
});

app.MapGet("/Courses/ICS/{id:int}", async (int id, ApplicationDbContext context) =>
{
    var course = await context.Courses.FindAsync(id);
    if (course == null)
    {
        return Results.NotFound();
    }

    var builder = new StringBuilder();
    builder.AppendLine("BEGIN:VCALENDAR");
    builder.AppendLine("VERSION:2.0");
    builder.AppendLine("PRODID:-//SysJaky_N//EN");
    builder.AppendLine("BEGIN:VEVENT");
    builder.AppendLine($"UID:{course.Id}@sysjaky_n");
    builder.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
    builder.AppendLine($"DTSTART;VALUE=DATE:{course.Date:yyyyMMdd}");
    builder.AppendLine($"DTEND;VALUE=DATE:{course.Date.AddDays(1):yyyyMMdd}");
    builder.AppendLine($"SUMMARY:{course.Title}");
    if (!string.IsNullOrWhiteSpace(course.Description))
    {
        builder.AppendLine($"DESCRIPTION:{course.Description}");
    }
    builder.AppendLine("END:VEVENT");
    builder.AppendLine("END:VCALENDAR");

    var bytes = Encoding.UTF8.GetBytes(builder.ToString());
    return Results.File(bytes, "text/calendar", $"{course.Title}.ics");
});

app.Run();
