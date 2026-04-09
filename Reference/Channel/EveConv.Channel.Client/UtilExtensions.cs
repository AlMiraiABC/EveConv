namespace EveConv.Channel.Client;

internal static class UtilExtensions
{
    public static (string Source, string EventName) ParseTopic(this string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return (string.Empty, string.Empty);
        }
        var sep = topic.IndexOf(':');
        if (sep <= 0)
        {
            return (topic, string.Empty);
        }
        var source = topic[..sep];
        var eventName = sep + 1 < topic.Length ? topic[(sep + 1)..] : string.Empty;
        return (source, eventName);
    }
}
