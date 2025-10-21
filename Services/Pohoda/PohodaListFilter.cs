using System;

namespace SysJaky_N.Services.Pohoda;

public sealed record PohodaListFilter
{
    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public string? VariableSymbol { get; init; }

    public string? Number { get; init; }

    public static PohodaListFilter ByNumber(string number)
        => new()
        {
            Number = number
        };
}
