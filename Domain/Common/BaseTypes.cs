namespace Chess.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    private readonly List<IDomainEvent> _events = new();

    public TId Id { get; protected set; } = default!;
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent e) => _events.Add(e);
    public void ClearEvents() => _events.Clear();

    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && Id.Equals(other.Id);
    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

// Marker: only aggregate roots get repositories.
public interface IAggregateRoot { }

public class DomainException(string message) : Exception(message);
