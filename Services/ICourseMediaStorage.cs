using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysJaky_N.Services;

public interface ICourseMediaStorage
{
    Task<string> SaveCoverImageAsync(int courseId, Stream imageStream, string contentType, CancellationToken cancellationToken = default);

    Task DeleteCoverImageAsync(int courseId, CancellationToken cancellationToken = default);
}
