using System.Collections.Generic;
using System.Text.Json;

namespace Supabase.Storage.VectorBuckets.Common;

/// <summary>
/// A vector as it is stored in an index: its key, and its data and metadata when the call asked for them.
/// </summary>
public sealed record StoredVector
{
    /// <summary>The key that identifies the vector within its index.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// The components of the vector and their type, or null when the call did not ask for them. Read float32
    /// components with <c>vector.Data?.Float32</c>.
    /// </summary>
    public VectorData? Data { get; init; }

    /// <summary>
    /// The metadata stored with the vector, or null when the call did not ask for it. Empty when the vector has none.
    /// Read a value with <see cref="JsonElement.GetString"/>, <see cref="JsonElement.GetDouble"/> or
    /// <see cref="JsonSerializer.Deserialize{TValue}(JsonElement, JsonSerializerOptions)"/>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}
