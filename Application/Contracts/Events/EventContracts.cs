using Chess.Domain.Common;

namespace Chess.Application.Contracts.Events;

public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent evt, CancellationToken ct);
}

public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}
