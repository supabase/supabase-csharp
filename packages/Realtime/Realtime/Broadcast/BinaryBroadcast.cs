namespace Supabase.Realtime.Broadcast;

/// <summary>
/// The payload of a push that goes out as a binary user broadcast frame. A JSON payload goes out this way when
/// it carries metadata.
/// </summary>
internal sealed class BinaryBroadcast
{
    internal BinaryBroadcast(string userEvent, byte[] payload, bool isJson = false, string metadata = "")
    {
        this.Event = userEvent;
        this.Payload = payload;
        this.IsJson = isJson;
        this.Metadata = metadata;
    }

    internal string Event { get; }

    internal byte[] Payload { get; }

    internal bool IsJson { get; }

    internal string Metadata { get; }
}
