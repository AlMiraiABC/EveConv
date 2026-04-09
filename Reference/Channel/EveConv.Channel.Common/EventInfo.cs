namespace EveConv.Channel.Common;

public record EventInfo<T>(string Source, string EventName, string Topic, T? Payload)
{
    public static EventInfo<V?> DownCast<V, F>(EventInfo<F?> e)
    {
        var payload = e.Payload switch
        {
            null => default,
            V v => v,
            _ => throw new InvalidCastException($"Failed to cast payload type {e.Payload.GetType()} to {typeof(V)}."),
        };
        return new(e.Source, e.EventName, e.Topic, payload);
    }
}
