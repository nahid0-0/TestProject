using Chess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chess.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> b)
    {
        b.ToTable("Games");
        b.HasKey(g => g.Id);
        b.Property(g => g.LichessGameId).HasMaxLength(16).IsRequired();
        b.HasIndex(g => g.LichessGameId).IsUnique();
        b.HasIndex(g => new { g.WhitePlayerId, g.PlayedAt });
        b.HasIndex(g => new { g.BlackPlayerId, g.PlayedAt });

        b.Property(g => g.Result).HasConversion<string>().HasMaxLength(16);
        b.Property(g => g.Termination).HasConversion<string>().HasMaxLength(16);

        b.ComplexProperty(g => g.TimeControl, tc =>
        {
            tc.Property(t => t.InitialSeconds).HasColumnName("TcInitial");
            tc.Property(t => t.IncrementSeconds).HasColumnName("TcIncrement");
        });

        b.HasMany(g => g.Moves).WithOne().HasForeignKey(m => m.GameId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(g => g.Moves).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MoveConfiguration : IEntityTypeConfiguration<Move>
{
    public void Configure(EntityTypeBuilder<Move> b)
    {
        b.ToTable("Moves");
        b.HasKey(m => new { m.GameId, m.Ply });
        b.Property(m => m.San).HasMaxLength(12);
        b.Property(m => m.Uci).HasMaxLength(6);
        b.Property(m => m.Phase).HasConversion<string>().HasMaxLength(16);
        b.Property(m => m.Classification).HasConversion<string>().HasMaxLength(16);

        b.ComplexProperty(m => m.Evaluation, e =>
        {
            e.Property(x => x.Centipawns).HasColumnName("EvalCp");
            e.Property(x => x.MateIn).HasColumnName("EvalMate");
        });
    }
}

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("Players");
        b.HasKey(p => p.Id);
        b.Property(p => p.LichessUsername).HasMaxLength(64).IsRequired();
        b.HasIndex(p => p.LichessUsername).IsUnique();
        b.Property(p => p.DisplayName).HasMaxLength(64);
        b.Property(p => p.Modules).HasConversion<int>();
    }
}

public sealed class OpeningConfiguration : IEntityTypeConfiguration<Opening>
{
    public void Configure(EntityTypeBuilder<Opening> b)
    {
        b.ToTable("Openings");
        b.HasKey(o => o.Id);
        b.Property(o => o.EcoCode).HasMaxLength(4).IsRequired();
        b.Property(o => o.Name).HasMaxLength(128).IsRequired();
        b.Property(o => o.StartingMovesUci).HasMaxLength(512);
        b.HasIndex(o => o.EcoCode);
    }
}

public sealed class SyncJobConfiguration : IEntityTypeConfiguration<SyncJob>
{
    public void Configure(EntityTypeBuilder<SyncJob> b)
    {
        b.ToTable("SyncJobs");
        b.HasKey(s => s.Id);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(s => s.Mode).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(s => new { s.PlayerId, s.Status });
        b.Property(s => s.Version).IsRowVersion();
    }
}

public sealed class RatingSnapshotConfiguration : IEntityTypeConfiguration<RatingSnapshot>
{
    public void Configure(EntityTypeBuilder<RatingSnapshot> b)
    {
        b.ToTable("RatingSnapshots");
        b.HasKey(r => r.Id);
        b.Property(r => r.Category).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(r => new { r.PlayerId, r.Category, r.RecordedAt });
    }
}

public sealed class AnalyticsSnapshotConfiguration : IEntityTypeConfiguration<AnalyticsSnapshot>
{
    public void Configure(EntityTypeBuilder<AnalyticsSnapshot> b)
    {
        b.ToTable("AnalyticsSnapshots");
        b.HasKey(a => a.Id);
        b.Property(a => a.Type).HasMaxLength(64).IsRequired();
        b.Property(a => a.FilterHash).HasMaxLength(64).IsRequired();
        b.HasIndex(a => new { a.PlayerId, a.Type, a.FilterHash }).IsUnique();
    }
}
