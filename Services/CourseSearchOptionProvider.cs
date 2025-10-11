using System;
using System.Linq;
using Microsoft.Extensions.Localization;

namespace SysJaky_N.Services
{
    public interface ICourseSearchOptionProvider
    {
        IReadOnlyList<string> Personas { get; }

        IReadOnlyList<string> Goals { get; }

        string PersonaPlaceholder { get; }

        string GoalPlaceholder { get; }
    }

    public class CourseSearchOptionProvider : ICourseSearchOptionProvider
    {
        private static readonly string[] PersonaKeys =
        {
            "Persona_Individual",
            "Persona_HRTeamLeader",
            "Persona_Laboratory",
            "Persona_QualityManager",
            "Persona_Auditor",
            "Persona_ISOBeginner"
        };

        private static readonly string[] GoalKeys =
        {
            "Goal_GetOrRenewCertificate",
            "Goal_QuicklyGainSkill",
            "Goal_Retrain",
            "Goal_TrainWholeTeam"
        };

        private readonly IStringLocalizer _localizer;

        public CourseSearchOptionProvider(IStringLocalizer<CourseSearchOptionProvider> localizer, IStringLocalizerFactory localizerFactory)
        {
            if (localizer is null)
            {
                throw new ArgumentNullException(nameof(localizer));
            }

            if (localizerFactory is null)
            {
                throw new ArgumentNullException(nameof(localizerFactory));
            }

            _localizer = ResolveLocalizer(localizer, localizerFactory);
        }

        public IReadOnlyList<string> Personas => PersonaKeys.Select(GetString).ToArray();

        public IReadOnlyList<string> Goals => GoalKeys.Select(GetString).ToArray();

        public string PersonaPlaceholder => GetString("PersonaPlaceholder");

        public string GoalPlaceholder => GetString("GoalPlaceholder");

        private string GetString(string key)
        {
            return _localizer[key].Value;
        }

        private static IStringLocalizer ResolveLocalizer(IStringLocalizer<CourseSearchOptionProvider> typedLocalizer, IStringLocalizerFactory localizerFactory)
        {
            var placeholder = typedLocalizer[nameof(PersonaPlaceholder)];
            if (!placeholder.ResourceNotFound)
            {
                return typedLocalizer;
            }

            var assemblyName = typeof(CourseSearchOptionProvider).Assembly.GetName().Name
                ?? throw new InvalidOperationException("Unable to determine assembly name for localization resources.");

            return localizerFactory.Create("Resources.Services.CourseSearchOptionProvider", assemblyName);
        }
    }
}
