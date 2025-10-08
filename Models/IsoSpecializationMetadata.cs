using System.Collections.Generic;

namespace SysJaky_N.Models;

public sealed record IsoSpecializationCourse(string Name, string Url);

public sealed record IsoSpecializationMetadata(
    string Id,
    string Name,
    string Tagline,
    string Description,
    string Icon,
    IReadOnlyList<IsoSpecializationCourse> Courses);
