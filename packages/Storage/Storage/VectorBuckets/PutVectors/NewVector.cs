using System.Collections.Generic;
using System.Text.Json;

namespace Supabase.Storage.VectorBuckets.PutVectors;

/// <summary>
/// A vector to store in an index: its key, its components and optional metadata. Storing a vector under a key that
/// is already in use replaces the vector stored under it.
/// </summary>
public sealed record NewVector
{
    /// <summary>The key that identifies the vector within its index. The service validates it.</summary>
    public required string Key { get; init; }

    /// <summary>The 32-bit floating point components of the vector. The service checks the count against the index dimension.</summary>
    public required IReadOnlyList<float> Data { get; init; }

    /// <summary>
    /// Values to store with the vector: strings, numbers, booleans or arrays of them. Left out when null.
    /// Build a value with <see cref="JsonSerializer.SerializeToElement{TValue}(TValue, JsonSerializerOptions)"/>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}
