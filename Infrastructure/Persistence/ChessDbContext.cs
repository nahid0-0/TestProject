using Chess.Application.Contracts.Events;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Common;
using Chess.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chess.Infrastructure.Persistence;

public sealed class ChessDbContext(DbContextOptions<ChessDbContext> options, IEventPublisher events)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Move> Moves => Set<Move>();
    public DbSet<Opening> Openings => Set<Opening>();
    public DbSet<RatingSnapshot> RatingSnapshots => Set<RatingSnapshot>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots => Set<AnalyticsSnapshot>();

    protected override void OnModelCreating(ModelBuilder mb) =>
        mb.ApplyConfigurationsFromAssembly(typeof(ChessDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var pending = ChangeTracker.Entries()
            .Select(e => e.Entity)
            .OfType<Entity<Guid>>()
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var all = pending.SelectMany(e => e.DomainEvents).ToList();
        pending.ForEach(e => e.ClearEvents());

        var rows = await base.SaveChangesAsync(ct);
        foreach (var evt in all)
        {
            await events.PublishAsync(evt, ct);   // after commit
        }
        return rows;
    }
}
