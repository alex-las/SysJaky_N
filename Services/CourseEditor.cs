using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SysJaky_N.Services;

public sealed class CourseEditor : ICourseEditor
{
    private readonly ICourseMediaStorage _courseMediaStorage;
    private readonly ICacheService _cacheService;

    public CourseEditor(ICourseMediaStorage courseMediaStorage, ICacheService cacheService)
    {
        _courseMediaStorage = courseMediaStorage;
        _cacheService = cacheService;
    }

    public bool ValidateCoverImage(IFormFile? coverImage, ModelStateDictionary modelState, string fieldName)
    {
        var isValid = true;

        if (coverImage is { Length: > 0 } && !string.Equals(coverImage.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            modelState.AddModelError(fieldName, "Nahrajte prosím obálku ve formátu JPEG.");
            isValid = false;
        }
        else if (coverImage is { Length: 0 })
        {
            modelState.AddModelError(fieldName, "Soubor s obálkou je prázdný.");
            isValid = false;
        }

        return isValid;
    }

    public async Task<CourseCoverImageResult> SaveCoverImageAsync(
        int courseId,
        IFormFile? coverImage,
        CancellationToken cancellationToken)
    {
        if (coverImage is not { Length: > 0 })
        {
            return CourseCoverImageResult.NoChange;
        }

        try
        {
            await using var imageStream = coverImage.OpenReadStream();
            var coverUrl = await _courseMediaStorage.SaveCoverImageAsync(
                courseId,
                imageStream,
                coverImage.ContentType,
                cancellationToken);
            return CourseCoverImageResult.Success(coverUrl);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return CourseCoverImageResult.Failure("Nepodařilo se uložit obálku kurzu. Zkontrolujte prosím soubor a zkuste to znovu.");
        }
    }

    public void InvalidateCourseCache(int courseId)
    {
        _cacheService.InvalidateCourseList();
        _cacheService.InvalidateCourseDetail(courseId);
    }
}

public sealed record CourseCoverImageResult(bool Succeeded, string? CoverImageUrl, string? ErrorMessage)
{
    public static CourseCoverImageResult Success(string coverImageUrl) => new(true, coverImageUrl, null);

    public static CourseCoverImageResult Failure(string errorMessage) => new(false, null, errorMessage);

    public static CourseCoverImageResult NoChange { get; } = new(true, null, null);
}
