using Chess.Application.Common;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Entities;

namespace Chess.Application.Services;

public sealed class PlayerService(IPlayerRepository players, IUnitOfWork uow)
{
    public async Task<Player> RegisterAsync(string username, CancellationToken ct)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var existing = await players.GetByUsernameAsync(normalized, ct);
        if (existing is not null)
        {
            if (!existing.IsTracked)
            {
                existing.PromoteToTracked();
                await uow.SaveChangesAsync(ct);
            }
            return existing;
        }

        var player = Player.Track(normalized);
        await players.AddAsync(player, ct);
        await uow.SaveChangesAsync(ct);
        return player;
    }

    public async Task<Player> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await players.GetAsync(id, ct) ?? throw new NotFoundException($"Player with ID '{id}' was not found.");
    }

    public async Task<IReadOnlyList<Player>> ListTrackedAsync(CancellationToken ct)
    {
        return await players.ListTrackedAsync(ct);
    }
}
