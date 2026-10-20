using System.Data;
using System.Data.Common;
using Chess.Application.Sync.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chess.Infrastructure.Persistence.Locks;

public sealed class PostgresAdvisoryLock(ChessDbContext db) : IPlayerSyncLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid playerId, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        long lockId = (long)(playerId.GetHashCode() & 0x7FFFFFFF);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
        var param = cmd.CreateParameter();
        param.ParameterName = "@key";
        param.Value = lockId;
        cmd.Parameters.Add(param);

        var result = await cmd.ExecuteScalarAsync(ct);
        bool acquired = result is bool b && b;

        if (!acquired) return null;

        return new LockReleaser(conn, lockId);
    }

    private sealed class LockReleaser(DbConnection conn, long lockId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (conn.State == ConnectionState.Open)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT pg_advisory_unlock(@key);";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@key";
                    param.Value = lockId;
                    cmd.Parameters.Add(param);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                // Suppress release failure on teardown
            }
        }
    }
}
