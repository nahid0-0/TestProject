using System.Text;
using System.Text.Json;
using Chess.Application.Analytics.Contracts;
using Chess.Application.Common;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chess.Application.Export;

public sealed record ExportRequest(Guid PlayerId, string Format, string Dataset, AnalyticsFilter Filter);

public interface IExporter
{
    string Format { get; }          // "csv", "json", "pgn"
    string ContentType { get; }
    string FileExtension { get; }
    Task ExportAsync(ExportRequest request, Stream output, CancellationToken ct);
}

public abstract class GameExporterBase(IGameRepository games) : IExporter
{
    public abstract string Format { get; }
    public abstract string ContentType { get; }
    public abstract string FileExtension { get; }

    public async Task ExportAsync(ExportRequest r, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await WriteHeaderAsync(writer, ct);

        await foreach (var g in StreamGames(r, ct))
        {
            await WriteGameAsync(writer, g, ct);
        }

        await WriteFooterAsync(writer, ct);
        await writer.FlushAsync(ct);
    }

    protected virtual Task WriteHeaderAsync(StreamWriter w, CancellationToken ct) => Task.CompletedTask;
    protected abstract Task WriteGameAsync(StreamWriter w, Game g, CancellationToken ct);
    protected virtual Task WriteFooterAsync(StreamWriter w, CancellationToken ct) => Task.CompletedTask;

    private IAsyncEnumerable<Game> StreamGames(ExportRequest r, CancellationToken ct) =>
        games.Query().Include(g => g.Moves)
             .Where(g => g.WhitePlayerId == r.PlayerId || g.BlackPlayerId == r.PlayerId)
             .OrderBy(g => g.PlayedAt).AsAsyncEnumerable();
}

public sealed class PgnExporter(IGameRepository games) : GameExporterBase(games)
{
    public override string Format => "pgn";
    public override string ContentType => "application/x-chess-pgn";
    public override string FileExtension => ".pgn";

    protected override async Task WriteGameAsync(StreamWriter w, Game g, CancellationToken ct)
    {
        await w.WriteLineAsync($"[Event \"{(g.Rated ? "Rated" : "Casual")} {g.TimeControl}\"]");
        await w.WriteLineAsync($"[Site \"https://lichess.org/{g.LichessGameId}\"]");
        await w.WriteLineAsync($"[Date \"{g.PlayedAt:yyyy.MM.dd}\"]");
        await w.WriteLineAsync($"[Result \"{PgnResult(g.Result)}\"]");
        await w.WriteLineAsync();
        var moveStrings = g.Moves.OrderBy(m => m.Ply).Select(m =>
            m.Ply % 2 == 1 ? $"{(m.Ply + 1) / 2}. {m.San}" : m.San);
        await w.WriteLineAsync(string.Join(' ', moveStrings) + " " + PgnResult(g.Result));
        await w.WriteLineAsync();
    }

    private static string PgnResult(GameResult r) => r switch
    {
        GameResult.WhiteWin => "1-0",
        GameResult.BlackWin => "0-1",
        GameResult.Draw => "1/2-1/2",
        _ => "*"
    };
}

public sealed class CsvGamesExporter(IGameRepository games) : GameExporterBase(games)
{
    public override string Format => "csv";
    public override string ContentType => "text/csv";
    public override string FileExtension => ".csv";

    protected override async Task WriteHeaderAsync(StreamWriter w, CancellationToken ct)
    {
        await w.WriteLineAsync("GameId,PlayedAt,Result,Termination,TimeControl,Rated,WhiteRating,BlackRating,MovesCount");
    }

    protected override async Task WriteGameAsync(StreamWriter w, Game g, CancellationToken ct)
    {
        var line = $"{g.LichessGameId},{g.PlayedAt:O},{g.Result},{g.Termination},{g.TimeControl},{g.Rated},{g.WhiteRating},{g.BlackRating},{g.Moves.Count}";
        await w.WriteLineAsync(line);
    }
}

public sealed class JsonAnalyticsExporter(IAnalyticsSnapshotStore snapshotStore) : IExporter
{
    public string Format => "json";
    public string ContentType => "application/json";
    public string FileExtension => ".json";

    public async Task ExportAsync(ExportRequest request, Stream output, CancellationToken ct)
    {
        var snapshot = await snapshotStore.TryGetAsync(request.PlayerId, request.Dataset, request.Filter.Hash(), typeof(object), ct);
        await JsonSerializer.SerializeAsync(output, snapshot ?? new { message = "No computed analytics snapshot found" }, cancellationToken: ct);
    }
}

public sealed class ExporterFactory(IEnumerable<IExporter> exporters)
{
    public IExporter For(string format) =>
        exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException($"No exporter for '{format}'");
}
