using Chess.Application.Contracts.External;
using Chess.Domain.Entities;

namespace Chess.Application.Ingestion.Contracts;

public sealed class IngestionContext
{
    public required Player Player { get; init; }
    public required SyncJob Job { get; init; }
    public List<RawGame> Raw { get; } = new();
    public List<ParsedGame> Parsed { get; } = new();
    public List<GameDraft> Drafts { get; } = new();
    public List<Game> ToPersist { get; } = new();
    public List<IngestionError> Errors { get; } = new();
    public long MaxTimestampMs { get; set; }
}

public sealed record IngestionError(string LichessGameId, string Stage, string Message);

public interface IIngestionStage
{
    int Order { get; }
    string Name { get; }
    Task ExecuteAsync(IngestionContext ctx, CancellationToken ct);
}

public sealed class IngestionOptions
{
    public int BatchSize { get; set; } = 500;
}
