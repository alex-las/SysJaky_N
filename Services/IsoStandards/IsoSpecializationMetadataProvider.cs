using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using SysJaky_N.Resources;

namespace SysJaky_N.Services.IsoStandards;

public sealed class IsoSpecializationMetadataProvider : IIsoSpecializationMetadataProvider
{
    private readonly IStringLocalizer<IsoSpecializationResources> _localizer;

    private static readonly IReadOnlyList<IsoSpecializationDefinition> Definitions = new[]
    {
        new IsoSpecializationDefinition(
            Key: "ISO9001",
            CardIconClass: "bi bi-award-fill",
            OverviewIconClass: "bi-patch-check-fill",
            CardGradient: "linear-gradient(135deg, #2563eb 0%, #3b82f6 100%)",
            IconColorClass: "iso-9001",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-primary",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO9001.Course.Introduction", "/Courses/Index?search=ISO%209001"),
                new IsoSpecializationCourseDefinition("ISO9001.Course.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality"),
                new IsoSpecializationCourseDefinition("ISO9001.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20kvality")
            }),
        new IsoSpecializationDefinition(
            Key: "ISO14001",
            CardIconClass: "bi bi-tree-fill",
            OverviewIconClass: "bi-globe2",
            CardGradient: "linear-gradient(135deg, #10b981 0%, #34d399 100%)",
            IconColorClass: "iso-14001",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-success",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO14001.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20EMS"),
                new IsoSpecializationCourseDefinition("ISO14001.Course.IntegratedManager", "/Courses/Index?search=Mana%C5%BEer%20integrovan%C3%A9ho%20syst%C3%A9mu")
            }),
        new IsoSpecializationDefinition(
            Key: "ISOIEC17025",
            CardIconClass: "bi bi-clipboard-data-fill",
            OverviewIconClass: "bi-diagram-3",
            CardGradient: "linear-gradient(135deg, #8b5cf6 0%, #a78bfa 100%)",
            IconColorClass: "iso-17025",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: "ISOIEC17025.IconLabel",
            AboutIconColorClass: "text-info",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISOIEC17025.Course.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20laborato%C5%99e"),
                new IsoSpecializationCourseDefinition("ISOIEC17025.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20laborato%C5%99e"),
                new IsoSpecializationCourseDefinition("ISOIEC17025.Course.Metrologist", "/Courses/Index?search=Metrolog")
            }),
        new IsoSpecializationDefinition(
            Key: "ISO15189",
            CardIconClass: "bi bi-hospital-fill",
            OverviewIconClass: "bi-hospital",
            CardGradient: "linear-gradient(135deg, #ef4444 0%, #f87171 100%)",
            IconColorClass: "iso-15189",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-danger",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO15189.Course.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20zdravotnick%C3%A9%20laborato%C5%99e"),
                new IsoSpecializationCourseDefinition("ISO15189.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20zdravotnick%C3%A9%20laborato%C5%99e")
            }),
        new IsoSpecializationDefinition(
            Key: "HACCP",
            CardIconClass: "bi bi-egg-fill",
            OverviewIconClass: "bi-diagram-3",
            CardGradient: "linear-gradient(135deg, #f59e0b 0%, #fbbf24 100%)",
            IconColorClass: "haccp",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-warning",
            IncludeInOverview: false,
            Courses: new IsoSpecializationCourseDefinition[0]),
        new IsoSpecializationDefinition(
            Key: "ISO45001",
            CardIconClass: "bi bi-shield-fill-check",
            OverviewIconClass: "bi-shield-plus",
            CardGradient: "linear-gradient(135deg, #eab308 0%, #facc15 100%)",
            IconColorClass: "iso-45001",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-warning",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO45001.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20BOZP"),
                new IsoSpecializationCourseDefinition("ISO45001.Course.IntegratedManager", "/Courses/Index?search=Mana%C5%BEer%20integrovan%C3%A9ho%20syst%C3%A9mu")
            }),
        new IsoSpecializationDefinition(
            Key: "ISO27001",
            CardIconClass: "bi bi-shield-lock-fill",
            OverviewIconClass: "bi-shield-lock-fill",
            CardGradient: "linear-gradient(135deg, #6366f1 0%, #818cf8 100%)",
            IconColorClass: "iso-27001",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-primary",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO27001.Course.Introduction", "/Courses/Index?search=ISO%2027001"),
                new IsoSpecializationCourseDefinition("ISO27001.Course.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20ISMS")
            }),
        new IsoSpecializationDefinition(
            Key: "IATF16949",
            CardIconClass: "bi bi-car-front-fill",
            OverviewIconClass: "bi-gear-wide-connected",
            CardGradient: "linear-gradient(135deg, #475569 0%, #64748b 100%)",
            IconColorClass: "iatf-16949",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-dark",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("IATF16949.Course.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20automotive"),
                new IsoSpecializationCourseDefinition("IATF16949.Course.APQP", "/Courses/Index?search=APQP%20PPAP"),
                new IsoSpecializationCourseDefinition("IATF16949.Course.CoreTools", "/Courses/Index?search=Core%20Tools")
            }),
        new IsoSpecializationDefinition(
            Key: "ISO13485",
            CardIconClass: "bi bi-heart-pulse-fill",
            OverviewIconClass: "bi-heart-pulse-fill",
            CardGradient: "linear-gradient(135deg, #ec4899 0%, #f472b6 100%)",
            IconColorClass: "iso-13485",
            SecondaryIconClass: "bi bi-award-fill",
            IconLabelKey: null,
            AboutIconColorClass: "text-danger",
            IncludeInOverview: true,
            Courses: new[]
            {
                new IsoSpecializationCourseDefinition("ISO13485.Course.Introduction", "/Courses/Index?search=ISO%2013485"),
                new IsoSpecializationCourseDefinition("ISO13485.Course.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20zdravotnick%C3%BDch%20prost%C5%99edk%C5%AF")
            })
    };

    public IsoSpecializationMetadataProvider(IStringLocalizer<IsoSpecializationResources> localizer)
    {
        _localizer = localizer;
    }

    public IReadOnlyList<IsoSpecializationMetadata> GetAll()
    {
        return Definitions
            .Select(CreateMetadata)
            .ToList();
    }

    private IsoSpecializationMetadata CreateMetadata(IsoSpecializationDefinition definition)
    {
        var name = _localizer[$"{definition.Key}.Name"].Value;
        var summary = _localizer[$"{definition.Key}.Summary"].Value;
        var detailsLocalized = _localizer[$"{definition.Key}.Details"];
        var details = !string.IsNullOrWhiteSpace(detailsLocalized.Value) && !detailsLocalized.ResourceNotFound
            ? detailsLocalized.Value
            : summary;

        var iconLabel = definition.IconLabelKey is null
            ? name
            : GetOptionalValue(definition.IconLabelKey) ?? name;

        var courses = definition.Courses
            .Select(course => new IsoSpecializationCourse(
                _localizer[course.NameKey].Value,
                course.Url))
            .ToList();

        return new IsoSpecializationMetadata(
            definition.Key,
            name,
            summary,
            details,
            definition.CardIconClass,
            definition.OverviewIconClass,
            definition.CardGradient,
            definition.IconColorClass,
            definition.SecondaryIconClass,
            iconLabel,
            definition.AboutIconColorClass,
            courses,
            definition.IncludeInOverview);
    }

    private string? GetOptionalValue(string key)
    {
        var localized = _localizer[key];
        return localized.ResourceNotFound ? null : localized.Value;
    }

    private sealed record IsoSpecializationDefinition(
        string Key,
        string CardIconClass,
        string OverviewIconClass,
        string CardGradient,
        string IconColorClass,
        string SecondaryIconClass,
        string? IconLabelKey,
        string? AboutIconColorClass,
        bool IncludeInOverview,
        IReadOnlyList<IsoSpecializationCourseDefinition> Courses);

    private sealed record IsoSpecializationCourseDefinition(string NameKey, string Url);
}
