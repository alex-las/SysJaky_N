using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface IIsoSpecializationCatalog
{
    IReadOnlyList<IsoSpecializationMetadata> GetAll();
}
