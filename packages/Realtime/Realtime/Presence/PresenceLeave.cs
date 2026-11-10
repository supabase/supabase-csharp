using System.Collections.Generic;
using Supabase.Realtime.Models;

namespace Supabase.Realtime.Presence;

/// <summary>
/// Presences that left under one key.
/// </summary>
public sealed class PresenceLeave<TPresenceModel> where TPresenceModel : BasePresence
{
    internal PresenceLeave(string key, IReadOnlyList<TPresenceModel> currentPresences, IReadOnlyList<TPresenceModel> leftPresences)
    {
        this.Key = key;
        this.CurrentPresences = currentPresences;
        this.LeftPresences = leftPresences;
    }

    /// <summary>
    /// The presence key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The presences the key still has, empty when the key is gone.
    /// </summary>
    public IReadOnlyList<TPresenceModel> CurrentPresences { get; }

    /// <summary>
    /// The presences that left.
    /// </summary>
    public IReadOnlyList<TPresenceModel> LeftPresences { get; }
}
