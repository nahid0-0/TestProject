using Chess.Domain.Common;

namespace Chess.Domain.Entities;

public sealed class Opening : Entity<Guid>
{
    public string EcoCode { get; init; } = "";    // A00..E99
    public string Name { get; init; } = "";
    public string? Variation { get; init; }
    public string StartingMovesUci { get; init; } = "";  // space-separated, used for prefix match

    public Opening() { }

    public Opening(Guid id, string ecoCode, string name, string? variation, string startingMovesUci)
    {
        Id = id;
        EcoCode = ecoCode;
        Name = name;
        Variation = variation;
        StartingMovesUci = startingMovesUci;
    }
}
