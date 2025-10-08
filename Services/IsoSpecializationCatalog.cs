using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using SysJaky_N.Models;
using SysJaky_N.Resources;

namespace SysJaky_N.Services;

public sealed class IsoSpecializationCatalog : IIsoSpecializationCatalog
{
    private readonly IStringLocalizer<IsoSpecializationResources> _localizer;

    private static readonly IReadOnlyList<IsoSpecializationDefinition> Definitions = new List<IsoSpecializationDefinition>
    {
        new(
            Id: "iso-9001",
            Icon: "bi-patch-check-fill",
            NameKey: "ISO9001.Name",
            TaglineKey: "ISO9001.Tagline",
            DescriptionKey: "ISO9001.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO9001.Courses.Introduction", "/Courses/Index?search=ISO%209001"),
                new("ISO9001.Courses.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality"),
                new("ISO9001.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20kvality"),
            }),
        new(
            Id: "iso-14001",
            Icon: "bi-globe2",
            NameKey: "ISO14001.Name",
            TaglineKey: "ISO14001.Tagline",
            DescriptionKey: "ISO14001.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO14001.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20EMS"),
                new("ISO14001.Courses.IntegratedManager", "/Courses/Index?search=Mana%C5%BEer%20integrovan%C3%A9ho%20syst%C3%A9mu"),
            }),
        new(
            Id: "iso-17025",
            Icon: "bi-diagram-3",
            NameKey: "ISO17025.Name",
            TaglineKey: "ISO17025.Tagline",
            DescriptionKey: "ISO17025.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO17025.Courses.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20laborato%C5%99e"),
                new("ISO17025.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20laborato%C5%99e"),
                new("ISO17025.Courses.Metrologist", "/Courses/Index?search=Metrolog"),
            }),
        new(
            Id: "iso-15189",
            Icon: "bi-hospital",
            NameKey: "ISO15189.Name",
            TaglineKey: "ISO15189.Tagline",
            DescriptionKey: "ISO15189.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO15189.Courses.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20zdravotnick%C3%A9%20laborato%C5%99e"),
                new("ISO15189.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20zdravotnick%C3%A9%20laborato%C5%99e"),
            }),
        new(
            Id: "haccp",
            Icon: "bi-egg-fill",
            NameKey: "HACCP.Name",
            TaglineKey: "HACCP.Tagline",
            DescriptionKey: "HACCP.Description",
            Courses: new List<CourseDefinition>
            {
                new("HACCP.Courses.Implementation", "/Courses/Index?search=HACCP"),
                new("HACCP.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20HACCP"),
            }),
        new(
            Id: "iso-45001",
            Icon: "bi-shield-plus",
            NameKey: "ISO45001.Name",
            TaglineKey: "ISO45001.Tagline",
            DescriptionKey: "ISO45001.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO45001.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20BOZP"),
                new("ISO45001.Courses.IntegratedManager", "/Courses/Index?search=Mana%C5%BEer%20integrovan%C3%A9ho%20syst%C3%A9mu"),
            }),
        new(
            Id: "iso-27001",
            Icon: "bi-shield-lock-fill",
            NameKey: "ISO27001.Name",
            TaglineKey: "ISO27001.Tagline",
            DescriptionKey: "ISO27001.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO27001.Courses.Introduction", "/Courses/Index?search=ISO%2027001"),
                new("ISO27001.Courses.InternalAuditor", "/Courses/Index?search=Intern%C3%AD%20auditor%20ISMS"),
            }),
        new(
            Id: "iatf-16949",
            Icon: "bi-gear-wide-connected",
            NameKey: "IATF16949.Name",
            TaglineKey: "IATF16949.Tagline",
            DescriptionKey: "IATF16949.Description",
            Courses: new List<CourseDefinition>
            {
                new("IATF16949.Courses.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20automotive"),
                new("IATF16949.Courses.APQP", "/Courses/Index?search=APQP%20PPAP"),
                new("IATF16949.Courses.CoreTools", "/Courses/Index?search=Core%20Tools"),
            }),
        new(
            Id: "iso-13485",
            Icon: "bi-heart-pulse-fill",
            NameKey: "ISO13485.Name",
            TaglineKey: "ISO13485.Tagline",
            DescriptionKey: "ISO13485.Description",
            Courses: new List<CourseDefinition>
            {
                new("ISO13485.Courses.Introduction", "/Courses/Index?search=ISO%2013485"),
                new("ISO13485.Courses.QualityManager", "/Courses/Index?search=Mana%C5%BEer%20kvality%20zdravotnick%C3%BDch%20prost%C5%99edk%C5%AF"),
            }),
    };

    public IsoSpecializationCatalog(IStringLocalizer<IsoSpecializationResources> localizer)
    {
        _localizer = localizer;
    }

    public IReadOnlyList<IsoSpecializationMetadata> GetAll()
    {
        return Definitions
            .Select(definition => new IsoSpecializationMetadata(
                definition.Id,
                Localize(definition.NameKey),
                Localize(definition.TaglineKey),
                Localize(definition.DescriptionKey),
                definition.Icon,
                definition.Courses
                    .Select(course => new IsoSpecializationCourse(Localize(course.ResourceKey), course.Url))
                    .ToList()))
            .ToList();
    }

    private string Localize(string key) => _localizer[key].Value;

    private sealed record IsoSpecializationDefinition(
        string Id,
        string Icon,
        string NameKey,
        string TaglineKey,
        string DescriptionKey,
        IReadOnlyList<CourseDefinition> Courses);

    private sealed record CourseDefinition(string ResourceKey, string Url);
}
