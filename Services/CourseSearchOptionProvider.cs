using System.Collections.ObjectModel;
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
        private static readonly IReadOnlyList<string> PersonaResourceKeys = new ReadOnlyCollection<string>(new[]
        {
            "Persona_Individual",
            "Persona_HRTeamLeader",
            "Persona_Laboratory",
            "Persona_QualityManager",
            "Persona_Auditor",
            "Persona_ISOBeginner"
        });

        private static readonly IReadOnlyList<string> GoalResourceKeys = new ReadOnlyCollection<string>(new[]
        {
            "Goal_GetCertificate",
            "Goal_UpdateSkill",
            "Goal_Retrain",
            "Goal_TeamTraining"
        });

        private readonly IStringLocalizer<CourseSearchOptionProvider> _localizer;

        public CourseSearchOptionProvider(IStringLocalizer<CourseSearchOptionProvider> localizer)
        {
            _localizer = localizer;
        }

        public IReadOnlyList<string> Personas => PersonaResourceKeys
            .Select(key => _localizer[key].Value)
            .ToArray();

        public IReadOnlyList<string> Goals => GoalResourceKeys
            .Select(key => _localizer[key].Value)
            .ToArray();

        public string PersonaPlaceholder => _localizer["PersonaPlaceholder"].Value;

        public string GoalPlaceholder => _localizer["GoalPlaceholder"].Value;
    }
}
