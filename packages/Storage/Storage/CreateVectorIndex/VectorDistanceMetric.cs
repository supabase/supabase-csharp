namespace Supabase.Storage.CreateVectorIndex;

/// <summary>
/// The measure an index uses to rank vectors by similarity.
/// </summary>
public enum VectorDistanceMetric
{
    /// <summary>Cosine distance.</summary>
    Cosine,

    /// <summary>Euclidean distance.</summary>
    Euclidean,
}
