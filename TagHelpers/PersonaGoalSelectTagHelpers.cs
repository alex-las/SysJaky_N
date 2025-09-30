using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SysJaky_N.Services;

namespace SysJaky_N.TagHelpers
{
    [HtmlTargetElement("select", Attributes = PersonaOptionsAttributeName)]
    public class PersonaSelectTagHelper : TagHelper
    {
        private const string PersonaOptionsAttributeName = "persona-options";
        private readonly ICourseSearchOptionProvider _optionsProvider;

        public PersonaSelectTagHelper(ICourseSearchOptionProvider optionsProvider)
        {
            _optionsProvider = optionsProvider;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagMode = TagMode.StartTagAndEndTag;
            var encoder = HtmlEncoder.Default;
            var builder = new StringBuilder();

            builder.AppendLine($"<option value=\"\">{encoder.Encode(_optionsProvider.PersonaPlaceholder)}</option>");

            foreach (var persona in _optionsProvider.Personas)
            {
                var encodedValue = encoder.Encode(persona);
                builder.AppendLine($"<option value=\"{encodedValue}\">{encodedValue}</option>");
            }

            output.Content.SetHtmlContent(builder.ToString());
        }
    }

    [HtmlTargetElement("select", Attributes = GoalOptionsAttributeName)]
    public class GoalSelectTagHelper : TagHelper
    {
        private const string GoalOptionsAttributeName = "goal-options";
        private readonly ICourseSearchOptionProvider _optionsProvider;

        public GoalSelectTagHelper(ICourseSearchOptionProvider optionsProvider)
        {
            _optionsProvider = optionsProvider;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagMode = TagMode.StartTagAndEndTag;
            var encoder = HtmlEncoder.Default;
            var builder = new StringBuilder();

            builder.AppendLine($"<option value=\"\">{encoder.Encode(_optionsProvider.GoalPlaceholder)}</option>");

            foreach (var goal in _optionsProvider.Goals)
            {
                var encodedValue = encoder.Encode(goal);
                builder.AppendLine($"<option value=\"{encodedValue}\">{encodedValue}</option>");
            }

            output.Content.SetHtmlContent(builder.ToString());
        }
    }
}
