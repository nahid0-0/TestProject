using Chess.Application.Analytics.Contracts;
using Chess.Domain.Entities;
using Chess.Domain.Enums;

namespace Chess.Api.Dtos;

public sealed record RegisterPlayerRequest(string Username);

public sealed record PlayerDto(
    Guid Id,
    string LichessUsername,
    string? DisplayName,
    DateTime CreatedAt,
    DateTime? LastSyncedAt,
    bool IsTracked,
    EnabledModules Modules)
{
    public static PlayerDto From(Player p) =>
        new(p.Id, p.LichessUsername, p.DisplayName, p.CreatedAt, p.LastSyncedAt, p.IsTracked, p.Modules);
}

public sealed record AnalyticsFilterQuery(
    DateTime? From = null,
    DateTime? To = null,
    TimeControlCategory? Category = null,
    Color? Color = null,
    bool? RatedOnly = null,
    int? OpponentRatingMin = null,
    int? OpponentRatingMax = null,
    string? Variant = "standard")
{
    public AnalyticsFilter ToFilter() => new()
    {
        From = From,
        To = To,
        Category = Category,
        Color = Color,
        RatedOnly = RatedOnly,
        OpponentRatingMin = OpponentRatingMin,
        OpponentRatingMax = OpponentRatingMax,
        Variant = Variant
    };
}
