using System.Collections.Generic;

namespace SysJaky_N.Services.IsoStandards;

public interface IIsoSpecializationMetadataProvider
{
    IReadOnlyList<IsoSpecializationMetadata> GetAll();
}
