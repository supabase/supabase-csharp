using System.Collections.Generic;
using Supabase.Realtime.Models;

namespace Supabase.Realtime.Presence;

/// <summary>
/// Presences that joined under one key.
/// </summary>
/// <typeparam name="TPresenceModel">A model representing expected payload.</typeparam>
public sealed class PresenceJoin<TPresenceModel> where TPresenceModel : BasePresence
{
    internal PresenceJoin(string key, IReadOnlyList<TPresenceModel> currentPresences, IReadOnlyList<TPresenceModel> newPresences)
    {
        this.Key = key;
        this.CurrentPresences = currentPresences;
        this.NewPresences = newPresences;
    }

    /// <summary>
    /// The presence key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The presences the key already had, empty for a new key.
    /// </summary>
    public IReadOnlyList<TPresenceModel> CurrentPresences { get; }

    /// <summary>
    /// The presences that joined.
    /// </summary>
    public IReadOnlyList<TPresenceModel> NewPresences { get; }
}
