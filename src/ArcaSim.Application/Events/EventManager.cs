using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ArcaSim.Application.Events;

/// <summary>Something that happened in ArcaSim that other parts may care about.</summary>
public interface IArcaSimEvent
{
    DateTimeOffset At { get; }
}

/// <summary>
/// The bus between the parts of ArcaSim: whoever acts publishes what happened,
/// whoever cares subscribes, and neither knows the other. Delivery is
/// synchronous and in subscription order; a listener that throws does not
/// stop the others, and its error goes to the log.
/// </summary>
public sealed class EventManager(ILogger<EventManager>? logger = null)
{
    private readonly ConcurrentDictionary<Type, ImmutableHandlers> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IArcaSimEvent =>
        _handlers.AddOrUpdate(typeof(TEvent),
            _ => new ImmutableHandlers([e => handler((TEvent)e)]),
            (_, current) => new ImmutableHandlers([.. current.Items, e => handler((TEvent)e)]));

    /// <summary>Every event, whatever its type: for listeners that keep a log of everything.</summary>
    public void SubscribeAll(Action<IArcaSimEvent> handler) =>
        _handlers.AddOrUpdate(typeof(IArcaSimEvent),
            _ => new ImmutableHandlers([handler]),
            (_, current) => new ImmutableHandlers([.. current.Items, handler]));

    public void Publish<TEvent>(TEvent @event) where TEvent : IArcaSimEvent
    {
        Deliver(@event.GetType(), @event);
        Deliver(typeof(IArcaSimEvent), @event);
    }

    private void Deliver(Type type, IArcaSimEvent @event)
    {
        if (!_handlers.TryGetValue(type, out var handlers)) return;
        foreach (var handler in handlers.Items)
        {
            try
            {
                handler(@event);
            }
            catch (Exception ex)
            {
                // A broken listener must not break the request that published the event.
                logger?.LogError(ex, "A listener of {Event} failed", type.Name);
            }
        }
    }

    private sealed record ImmutableHandlers(IReadOnlyList<Action<IArcaSimEvent>> Items);
}
