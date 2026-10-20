using System.Threading.Channels;
using Chess.Application.Common;
using Chess.Application.Contracts.External;
using Chess.Application.Contracts.Persistence;
using Chess.Application.Ingestion;
using Chess.Application.Sync.Contracts;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chess.Application.Sync;

public sealed class ChannelSyncScheduler : ISyncScheduler
{
    private readonly Channel<SyncRequest> _channel =
        Channel.CreateBounded<SyncRequest>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<SyncRequest> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid id, SyncMode mode, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(new SyncRequest(id, mode), ct);
}

public sealed class SyncWorker(
    ChannelSyncScheduler queue,
    IServiceScopeFactory scopes,
    IOptions<SyncOptions> options,
    ILogger<SyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var gate = new SemaphoreSlim(options.Value.MaxConcurrentSyncs);
        await foreach (var req in queue.Reader.ReadAllAsync(stop))
        {
            await gate.WaitAsync(stop);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<SyncOrchestrator>().RunAsync(req, stop);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Sync crashed for {Player}", req.PlayerId);
                }
                finally
                {
                    gate.Release();
                }
            }, stop);
        }
    }
}

public sealed class SyncOrchestrator(
    IPlayerRepository players,
    IRepository<SyncJob> jobs,
    IPlayerSyncLock locks,
    IngestionPipeline pipeline,
    IUnitOfWork uow,
    IClock clock)
{
    public async Task RunAsync(SyncRequest req, CancellationToken ct)
    {
        _ = clock;
        await using var handle = await locks.TryAcquireAsync(req.PlayerId, ct);
        if (handle is null) return;                          // SY-1: another sync is active

        var player = await players.GetAsync(req.PlayerId, ct) ?? throw new NotFoundException("Player");
        var job = SyncJob.Create(player.Id, req.Mode);
        await jobs.AddAsync(job, ct);
        job.Start();
        await uow.SaveChangesAsync(ct);

        try
        {
            var query = req.Mode == SyncMode.Backfill
                ? new GameQuery()
                : new GameQuery(SinceMs: player.LastGameTimestampMs);

            await pipeline.RunAsync(player, job, query, ct);
            job.Succeed();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.Fail(ex.Message);
        }
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class SyncSchedulerService(
    IServiceScopeFactory scopes,
    IOptions<SyncOptions> options,
    ILogger<SyncSchedulerService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ScheduledInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var players = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
                var scheduler = scope.ServiceProvider.GetRequiredService<ISyncScheduler>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var tracked = await players.ListTrackedAsync(stoppingToken);
                var staleThreshold = clock.UtcNow - options.Value.StaleThreshold;

                foreach (var player in tracked)
                {
                    if (player.LastSyncedAt == null || player.LastSyncedAt <= staleThreshold)
                    {
                        await scheduler.EnqueueAsync(player.Id, SyncMode.Incremental, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Scheduled sync check failed");
            }
        }
    }
}
