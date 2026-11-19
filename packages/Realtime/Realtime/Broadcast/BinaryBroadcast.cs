namespace Supabase.Realtime.Broadcast;

/// <summary>
/// The payload of a push that goes out as a binary user broadcast frame.
/// </summary>
internal sealed class BinaryBroadcast
{
    internal BinaryBroadcast(string userEvent, byte[] payload)
    {
        this.Event = userEvent;
        this.Payload = payload;
    }

    internal string Event { get; }

    internal byte[] Payload { get; }
}
