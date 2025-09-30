using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SysJaky_N.Services;

public interface ICourseEditor
{
    bool ValidateCoverImage(IFormFile? coverImage, ModelStateDictionary modelState, string fieldName);

    Task<CourseCoverImageResult> SaveCoverImageAsync(
        int courseId,
        IFormFile? coverImage,
        CancellationToken cancellationToken);

    void InvalidateCourseCache(int courseId);
}
