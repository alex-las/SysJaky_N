using System;

namespace SysJaky_N.Models;

public class ChatbotSettings
{
    public int Id { get; set; }

    /// <summary>
    /// Determines whether the chatbot widget should be rendered on the public site.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Controls if the chatbot JavaScript should automatically initialize on page load.
    /// </summary>
    public bool AutoInitialize { get; set; }

    /// <summary>
    /// The UTC timestamp of the last update so administrators know when the configuration changed.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
