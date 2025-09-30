using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Pages.Courses;
using SysJaky_N.Services;
using Xunit;

namespace SysJaky_N.Tests;

public class CoursePageModelTests
{
    [Fact]
    public async Task CreateModel_ReturnsPage_WhenValidationFails()
    {
        await using var fixture = await CourseFixture.CreateAsync();
        fixture.Context.CourseGroups.Add(new CourseGroup { Name = "Group" });
        await fixture.Context.SaveChangesAsync();
        var courseGroupId = fixture.Context.CourseGroups.Select(g => g.Id).First();

        var auditService = new Mock<IAuditService>();
        var editor = new Mock<ICourseEditor>();
        editor
            .Setup(e => e.ValidateCoverImage(It.IsAny<IFormFile?>(), It.IsAny<ModelStateDictionary>(), It.IsAny<string>()))
            .Callback<IFormFile?, ModelStateDictionary, string>((_, state, field) => state.AddModelError(field, "error"))
            .Returns(false);

        var model = new CreateModel(fixture.Context, auditService.Object, editor.Object)
        {
            Course = new Course
            {
                Title = "New Course",
                Description = "Desc",
                Date = DateTime.UtcNow,
                CourseGroupId = courseGroupId
            }
        };
        model.PageContext = new PageContext(new ActionContext
        {
            HttpContext = new DefaultHttpContext()
        });

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("CoverImage"));
        editor.Verify(e => e.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()), Times.Never);
        editor.Verify(e => e.InvalidateCourseCache(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateModel_ReturnsPage_WhenCoverSaveFails()
    {
        await using var fixture = await CourseFixture.CreateAsync();
        fixture.Context.CourseGroups.Add(new CourseGroup { Name = "Group" });
        await fixture.Context.SaveChangesAsync();
        var courseGroupId = fixture.Context.CourseGroups.Select(g => g.Id).First();

        var auditService = new Mock<IAuditService>();
        var editor = new Mock<ICourseEditor>();
        editor
            .Setup(e => e.ValidateCoverImage(It.IsAny<IFormFile?>(), It.IsAny<ModelStateDictionary>(), It.IsAny<string>()))
            .Returns(true);
        editor
            .Setup(e => e.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CourseCoverImageResult.Failure("cover error"));

        var model = new CreateModel(fixture.Context, auditService.Object, editor.Object)
        {
            Course = new Course
            {
                Title = "New Course",
                Description = "Desc",
                Date = DateTime.UtcNow,
                CourseGroupId = courseGroupId
            },
            CoverImage = CreateFormFile()
        };
        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext(new ActionContext
        {
            HttpContext = httpContext
        });

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, model.Course.Id);
        Assert.True(model.ModelState.ContainsKey("CoverImage"));
        editor.Verify(e => e.InvalidateCourseCache(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateModel_Redirects_OnSuccess()
    {
        await using var fixture = await CourseFixture.CreateAsync();
        fixture.Context.CourseGroups.Add(new CourseGroup { Name = "Group" });
        await fixture.Context.SaveChangesAsync();
        var courseGroupId = fixture.Context.CourseGroups.Select(g => g.Id).First();

        var auditService = new Mock<IAuditService>();
        auditService.Setup(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var editor = new Mock<ICourseEditor>();
        editor
            .Setup(e => e.ValidateCoverImage(It.IsAny<IFormFile?>(), It.IsAny<ModelStateDictionary>(), It.IsAny<string>()))
            .Returns(true);
        editor
            .Setup(e => e.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CourseCoverImageResult.Success("/covers/course-1.jpg"));

        var model = new CreateModel(fixture.Context, auditService.Object, editor.Object)
        {
            Course = new Course
            {
                Title = "New Course",
                Description = "Desc",
                Date = DateTime.UtcNow,
                CourseGroupId = courseGroupId
            },
            CoverImage = CreateFormFile()
        };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }))
        };
        model.PageContext = new PageContext(new ActionContext
        {
            HttpContext = httpContext
        });

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
        Assert.Single(fixture.Context.Courses);
        Assert.Equal("/covers/course-1.jpg", fixture.Context.Courses.Single().CoverImageUrl);
        editor.Verify(e => e.InvalidateCourseCache(It.IsAny<int>()), Times.Once);
        auditService.Verify(a => a.LogAsync("user-1", "CourseCreated", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EditModel_ReturnsPage_WhenCoverSaveFails()
    {
        await using var fixture = await CourseFixture.CreateAsync();
        var course = new Course
        {
            Title = "Existing",
            Description = "Desc",
            Date = DateTime.UtcNow,
            CoverImageUrl = "/covers/old.jpg",
            Level = CourseLevel.Beginner,
            Mode = CourseMode.SelfPaced,
            Duration = 10
        };
        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();

        var auditService = new Mock<IAuditService>();
        var editor = new Mock<ICourseEditor>();
        editor
            .Setup(e => e.ValidateCoverImage(It.IsAny<IFormFile?>(), It.IsAny<ModelStateDictionary>(), It.IsAny<string>()))
            .Returns(true);
        editor
            .Setup(e => e.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CourseCoverImageResult.Failure("cover error"));

        var model = new EditModel(fixture.Context, auditService.Object, editor.Object)
        {
            Course = new Course
            {
                Id = course.Id,
                Title = "Updated",
                Description = "Desc",
                Date = course.Date,
                Level = CourseLevel.Advanced,
                Mode = CourseMode.InstructorLed,
                Duration = 20
            },
            CoverImage = CreateFormFile()
        };
        model.PageContext = new PageContext(new ActionContext
        {
            HttpContext = new DefaultHttpContext()
        });

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("/covers/old.jpg", model.Course.CoverImageUrl);
        Assert.True(model.ModelState.ContainsKey("CoverImage"));
    }

    [Fact]
    public async Task EditModel_Redirects_OnSuccess()
    {
        await using var fixture = await CourseFixture.CreateAsync();
        var course = new Course
        {
            Title = "Existing",
            Description = "Desc",
            Date = DateTime.UtcNow,
            CoverImageUrl = "/covers/old.jpg",
            Level = CourseLevel.Beginner,
            Mode = CourseMode.SelfPaced,
            Duration = 10
        };
        fixture.Context.Courses.Add(course);
        await fixture.Context.SaveChangesAsync();

        var auditService = new Mock<IAuditService>();
        auditService.Setup(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var editor = new Mock<ICourseEditor>();
        editor
            .Setup(e => e.ValidateCoverImage(It.IsAny<IFormFile?>(), It.IsAny<ModelStateDictionary>(), It.IsAny<string>()))
            .Returns(true);
        editor
            .Setup(e => e.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CourseCoverImageResult.Success("/covers/new.jpg"));

        var model = new EditModel(fixture.Context, auditService.Object, editor.Object)
        {
            Course = new Course
            {
                Id = course.Id,
                Title = "Updated",
                Description = "Updated desc",
                MetaTitle = "Meta",
                MetaDescription = "Meta desc",
                OpenGraphImage = "/og.jpg",
                CourseGroupId = null,
                Price = 10,
                Date = course.Date,
                Level = CourseLevel.Advanced,
                Mode = CourseMode.InstructorLed,
                Duration = 20
            },
            CoverImage = CreateFormFile()
        };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }))
        };
        model.PageContext = new PageContext(new ActionContext
        {
            HttpContext = httpContext
        });

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
        var updatedCourse = fixture.Context.Courses.Single();
        Assert.Equal("Updated", updatedCourse.Title);
        Assert.Equal("Updated desc", updatedCourse.Description);
        Assert.Equal("/covers/new.jpg", updatedCourse.CoverImageUrl);
        Assert.Equal(CourseLevel.Advanced, updatedCourse.Level);
        Assert.Equal(CourseMode.InstructorLed, updatedCourse.Mode);
        Assert.Equal(20, updatedCourse.Duration);
        editor.Verify(e => e.InvalidateCourseCache(course.Id), Times.Once);
        auditService.Verify(a => a.LogAsync("user-1", "CourseEdited", It.IsAny<string>()), Times.Once);
    }

    private static IFormFile CreateFormFile()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        return new FormFile(stream, 0, stream.Length, "CoverImage", "cover.jpg")
        {
            ContentType = "image/jpeg"
        };
    }

    private sealed class CourseFixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; }
        private readonly SqliteConnection _connection;

        private CourseFixture(ApplicationDbContext context, SqliteConnection connection)
        {
            Context = context;
            _connection = connection;
        }

        public static async Task<CourseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new CourseFixture(context, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
