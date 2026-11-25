using System.Collections.Generic;
using System.Text.Json;

namespace Supabase.Storage.QueryVectors;

/// <summary>
/// A vector that matched a similarity search: its key, and its distance and metadata when the call asked for them.
/// </summary>
public sealed record VectorMatch
{
    /// <summary>The key that identifies the vector within its index.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// The distance from the query vector, in the metric of the index (smaller is closer), or null when the call did
    /// not ask for it.
    /// </summary>
    public float? Distance { get; init; }

    /// <summary>
    /// The metadata stored with the vector, or null when the call did not ask for it. Empty when the vector has none.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}
