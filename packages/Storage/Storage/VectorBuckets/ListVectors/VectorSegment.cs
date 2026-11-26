namespace Supabase.Storage.VectorBuckets.ListVectors;

/// <summary>
/// One of several equal parts of an index, to list the parts in parallel. Every vector belongs to exactly one part;
/// the service validates the range.
/// </summary>
/// <param name="Index">The zero-based position of this part, below <paramref name="Count"/>.</param>
/// <param name="Count">The number of parts the index is split into.</param>
public sealed record VectorSegment(int Index, int Count);
