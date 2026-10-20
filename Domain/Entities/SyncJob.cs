using Chess.Domain.Common;
using Chess.Domain.Enums;
using Chess.Domain.Events;

namespace Chess.Domain.Entities;

public sealed class SyncJob : Entity<Guid>, IAggregateRoot
{
    private SyncJob() { }

    public Guid PlayerId { get; private set; }
    public SyncMode Mode { get; private set; }
    public SyncStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public int GamesImported { get; private set; }
    public int GamesFailed { get; private set; }
    public int Attempt { get; private set; }
    public string? Error { get; private set; }
    public uint Version { get; private set; } // concurrency token (xmin in postgres)

    public static SyncJob Create(Guid playerId, SyncMode mode) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = playerId,
        Mode = mode,
        Status = SyncStatus.Pending
    };

    public void Start()
    {
        Guard(SyncStatus.Pending, SyncStatus.Failed);
        Status = SyncStatus.Running;
        StartedAt = DateTime.UtcNow;
        Attempt++;
        Raise(new SyncStarted(Id, PlayerId));
    }

    public void ReportProgress(int imported, int failed)
    {
        GamesImported = imported;
        GamesFailed = failed;
    }

    public void Succeed()
    {
        Guard(SyncStatus.Running);
        Status = GamesFailed > 0 ? SyncStatus.Partial : SyncStatus.Succeeded;
        FinishedAt = DateTime.UtcNow;
        Raise(new GamesImported(PlayerId, GamesImported));
    }

    public void Fail(string error)
    {
        Guard(SyncStatus.Running);
        Status = SyncStatus.Failed;
        Error = error;
        FinishedAt = DateTime.UtcNow;
        Raise(new SyncFailed(Id, PlayerId, error));
    }

    private void Guard(params SyncStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException($"Cannot transition from {Status}");
    }
}
