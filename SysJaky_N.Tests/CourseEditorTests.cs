using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using SysJaky_N.Services;
using Xunit;

namespace SysJaky_N.Tests;

public class CourseEditorTests
{
    [Fact]
    public void ValidateCoverImage_AddsError_ForNonJpeg()
    {
        var mediaStorage = new Mock<ICourseMediaStorage>();
        var cacheService = new Mock<ICacheService>();
        var editor = new CourseEditor(mediaStorage.Object, cacheService.Object);
        var modelState = new ModelStateDictionary();

        var formFile = CreateFormFile("image/png");

        var result = editor.ValidateCoverImage(formFile, modelState, "CoverImage");

        Assert.False(result);
        Assert.True(modelState.ContainsKey("CoverImage"));
        Assert.Single(modelState["CoverImage"].Errors);
    }

    [Fact]
    public async Task SaveCoverImageAsync_ReturnsFailure_WhenStorageThrows()
    {
        var mediaStorage = new Mock<ICourseMediaStorage>();
        var cacheService = new Mock<ICacheService>();
        mediaStorage
            .Setup(s => s.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("failed"));
        var editor = new CourseEditor(mediaStorage.Object, cacheService.Object);

        var formFile = CreateFormFile("image/jpeg");

        var result = await editor.SaveCoverImageAsync(1, formFile, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SaveCoverImageAsync_ReturnsCoverUrl_OnSuccess()
    {
        var mediaStorage = new Mock<ICourseMediaStorage>();
        var cacheService = new Mock<ICacheService>();
        mediaStorage
            .Setup(s => s.SaveCoverImageAsync(It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/covers/course-1.jpg");
        var editor = new CourseEditor(mediaStorage.Object, cacheService.Object);

        var formFile = CreateFormFile("image/jpeg");

        var result = await editor.SaveCoverImageAsync(1, formFile, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("/covers/course-1.jpg", result.CoverImageUrl);
    }

    [Fact]
    public void InvalidateCourseCache_CallsCacheService()
    {
        var mediaStorage = new Mock<ICourseMediaStorage>();
        var cacheService = new Mock<ICacheService>();
        var editor = new CourseEditor(mediaStorage.Object, cacheService.Object);

        editor.InvalidateCourseCache(5);

        cacheService.Verify(s => s.InvalidateCourseList(), Times.Once);
        cacheService.Verify(s => s.InvalidateCourseDetail(5), Times.Once);
    }

    private static IFormFile CreateFormFile(string contentType)
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var file = new FormFile(stream, 0, stream.Length, "CoverImage", "cover.jpg")
        {
            ContentType = contentType
        };
        return file;
    }
}
