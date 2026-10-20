using Chess.Domain.Enums;

namespace Chess.Domain.ValueObjects;

public readonly record struct TimeControl(int InitialSeconds, int IncrementSeconds)
{
    // Lichess rule: estimated duration = initial + 40 * increment
    public int EstimatedSeconds => InitialSeconds + 40 * IncrementSeconds;

    public TimeControlCategory Category => EstimatedSeconds switch
    {
        <= 29   => TimeControlCategory.UltraBullet,
        <= 179  => TimeControlCategory.Bullet,
        <= 479  => TimeControlCategory.Blitz,
        <= 1499 => TimeControlCategory.Rapid,
        _       => TimeControlCategory.Classical
    };

    public override string ToString() => $"{InitialSeconds}+{IncrementSeconds}";
}

public readonly record struct Rating
{
    public int Value { get; }
    public Rating(int value)
    {
        if (value is < 0 or > 4000) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
}

public sealed record Evaluation(int? Centipawns, int? MateIn)
{
    public bool IsMate => MateIn.HasValue;
    // Clamp mates to a large cp value so averages are usable.
    public int AsCentipawns(int mateValue = 10_000) =>
        MateIn is { } m ? Math.Sign(m) * mateValue : Centipawns ?? 0;
}
