using System.Collections.ObjectModel;

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
        private static readonly IReadOnlyList<string> _personas = new ReadOnlyCollection<string>(new[]
        {
            "Jednotlivec",
            "HR / týmový leader",
            "Laboratoř",
            "Manažer kvality",
            "Auditor",
            "Začátečník v ISO"
        });

        private static readonly IReadOnlyList<string> _goals = new ReadOnlyCollection<string>(new[]
        {
            "získat/obnovit certifikát",
            "rychle doplnit dovednost",
            "rekvalifikovat se",
            "školení pro celý tým"
        });

        public IReadOnlyList<string> Personas => _personas;

        public IReadOnlyList<string> Goals => _goals;

        public string PersonaPlaceholder => "Jsem…";

        public string GoalPlaceholder => "Chci…";
    }
}
