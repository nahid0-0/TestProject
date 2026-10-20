using Chess.Domain.Common;

namespace Chess.Domain.Events;

public sealed record PlayerRegistered(Guid PlayerId, string Username) : DomainEvent;
public sealed record SyncStarted(Guid JobId, Guid PlayerId) : DomainEvent;
public sealed record GamesImported(Guid PlayerId, int Count) : DomainEvent;
public sealed record SyncFailed(Guid JobId, Guid PlayerId, string Error) : DomainEvent;
public sealed record AnalyticsRecomputed(Guid PlayerId, string Key) : DomainEvent;
public sealed record ExportCompleted(Guid ExportId) : DomainEvent;
