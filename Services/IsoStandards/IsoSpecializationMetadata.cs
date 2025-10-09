using System.Collections.Generic;

namespace SysJaky_N.Services.IsoStandards;

public sealed record IsoSpecializationCourse(string Name, string Url);

public sealed record IsoSpecializationMetadata(
    string Key,
    string Name,
    string Summary,
    string Details,
    string CardIconClass,
    string OverviewIconClass,
    string CardGradient,
    string IconColorClass,
    string SecondaryIconClass,
    string? IconLabel,
    string? AboutIconColorClass,
    IReadOnlyList<IsoSpecializationCourse> Courses,
    bool IncludeInOverview);
