using System.Collections.Generic;

namespace SysJaky_N.ViewModels;

public class HeroViewModel
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public string? PrimaryCta { get; init; }
    public string? SecondaryCta { get; init; }
    public string? SearchPlaceholder { get; init; }
    public List<string> Chips { get; init; } = new();
}
