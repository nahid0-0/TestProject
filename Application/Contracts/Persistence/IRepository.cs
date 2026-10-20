using Chess.Domain.Common;

namespace Chess.Application.Contracts.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IRepository<T> where T : IAggregateRoot
{
    Task<T?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(T entity, CancellationToken ct);
    void Remove(T entity);
}
