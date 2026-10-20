using Chess.Application.Contracts.Events;
using Chess.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Chess.Infrastructure.Events;

public sealed class InProcessEventPublisher(IServiceProvider sp) : IEventPublisher
{
    public async Task PublishAsync(IDomainEvent evt, CancellationToken ct)
    {
        var handlerType = typeof(IEventHandler<>).MakeGenericType(evt.GetType());
        var handlers = sp.GetServices(handlerType);
        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            var method = handlerType.GetMethod(nameof(IEventHandler<IDomainEvent>.HandleAsync));
            if (method is not null)
            {
                var task = (Task)method.Invoke(handler, [evt, ct])!;
                await task;
            }
        }
    }
}
