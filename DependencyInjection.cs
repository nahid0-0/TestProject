using Chess.Application.Analytics;
using Chess.Application.Analytics.Calculators;
using Chess.Application.Analytics.Contracts;
using Chess.Application.Analytics.EventHandlers;
using Chess.Application.Common;
using Chess.Application.Comparison;
using Chess.Application.Comparison.Contracts;
using Chess.Application.Contracts.Events;
using Chess.Application.Contracts.External;
using Chess.Application.Contracts.Persistence;
using Chess.Application.Export;
using Chess.Application.Ingestion;
using Chess.Application.Ingestion.Contracts;
using Chess.Application.Ingestion.Stages;
using Chess.Application.Services;
using Chess.Application.Sync;
using Chess.Application.Sync.Contracts;
using Chess.Infrastructure.Events;
using Chess.Infrastructure.Lichess;
using Chess.Infrastructure.Persistence;
using Chess.Infrastructure.Persistence.Locks;
using Chess.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chess;

public static class DependencyInjection
{
    public static IServiceCollection AddChess(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddDbContext<ChessDbContext>(o =>
        {
            var conn = cfg.GetConnectionString("Chess");
            if (!string.IsNullOrEmpty(conn))
            {
                o.UseNpgsql(conn);
            }
        });

        s.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ChessDbContext>());
        s.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        s.AddScoped<IPlayerRepository, PlayerRepository>();
        s.AddScoped<IGameRepository, GameRepository>();
        s.AddScoped<IAnalyticsSnapshotStore, AnalyticsSnapshotStore>();

        // Ingestion: every stage registered against the same interface; pipeline sorts by Order.
        s.AddScoped<IIngestionStage, DeduplicateStage>();
        s.AddScoped<IIngestionStage, ParseStage>();
        s.AddScoped<IIngestionStage, ValidateStage>();
        s.AddScoped<IIngestionStage, EnrichStage>();
        s.AddScoped<IIngestionStage, ResolvePlayersStage>();
        s.AddScoped<IIngestionStage, BuildEntitiesStage>();
        s.AddScoped<IngestionPipeline>();
        s.AddSingleton<IGameParser, LichessGameParser>();
        s.AddSingleton<IOpeningClassifier, TrieOpeningClassifier>();   // loaded once at startup
        s.AddSingleton<IGamePhaseDetector, GamePhaseDetector>();
        s.AddSingleton<IMoveClassifier, MoveClassifier>();

        // Analytics: auto-register every calculator in the assembly.
        s.Scan(scan => scan.FromAssemblyOf<WinLossCalculator>()
            .AddClasses(c => c.AssignableTo<IAnalyticsCalculator>())
            .As<IAnalyticsCalculator>().WithScopedLifetime());
        s.Decorate<IAnalyticsCalculator, CachingCalculator>();
        s.AddScoped<IAnalyticsRegistry, AnalyticsRegistry>();

        // Comparison
        s.AddScoped<ComparisonService>();
        s.AddSingleton<ISignificanceTest, SignificanceTest>();
        s.AddScoped<IMetricExtractor, WinLossMetricExtractor>();
        s.AddScoped<ICohortBaselineProvider, CohortBaselineProvider>();

        // Exporters
        s.AddScoped<IExporter, PgnExporter>();
        s.AddScoped<IExporter, CsvGamesExporter>();
        s.AddScoped<IExporter, JsonAnalyticsExporter>();
        s.AddScoped<ExporterFactory>();

        // Sync
        s.AddSingleton<ChannelSyncScheduler>();
        s.AddSingleton<ISyncScheduler>(sp => sp.GetRequiredService<ChannelSyncScheduler>());
        s.AddScoped<IPlayerSyncLock, PostgresAdvisoryLock>();
        s.AddScoped<SyncOrchestrator>();
        s.AddHostedService<SyncWorker>();
        s.AddHostedService<SyncSchedulerService>();

        // Events
        s.AddScoped<IEventPublisher, InProcessEventPublisher>();   // resolves IEventHandler<T> from DI
        s.Scan(scan => scan.FromAssemblyOf<InvalidateAnalyticsOnGamesImported>()
            .AddClasses(c => c.AssignableTo(typeof(IEventHandler<>)))
            .AsImplementedInterfaces().WithScopedLifetime());

        s.AddSingleton<IClock, SystemClock>();
        s.AddScoped<PlayerService>();

        s.Configure<IngestionOptions>(cfg.GetSection("Ingestion"));
        s.Configure<SyncOptions>(cfg.GetSection("Sync"));
        s.Configure<AccuracyOptions>(cfg.GetSection("Accuracy"));

        // Lichess Client registration with resilience:
        s.AddTransient<LichessRateLimitHandler>();
        s.AddHttpClient<ILichessClient, LichessClient>(c =>
        {
            c.BaseAddress = new Uri("https://lichess.org/");
            c.Timeout = Timeout.InfiniteTimeSpan;        // streams are long-lived; use per-call CTS instead
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ChessAnalytics/1.0 (contact@example.com)");
        })
        .AddHttpMessageHandler<LichessRateLimitHandler>()
        .AddStandardResilienceHandler();

        return s;
    }
}
