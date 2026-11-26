using System;

namespace Supabase.Storage.VectorBuckets.QueryVectors;

/// <summary>
/// What the service returns with each match besides its key. Combine the values to ask for several, such as
/// <c>VectorMatchContent.Distance | VectorMatchContent.Metadata</c>. A query never returns the components of a vector.
/// </summary>
[Flags]
public enum VectorMatchContent
{
    /// <summary>Nothing beyond the key is asked for; the service applies its own default.</summary>
    None = 0,

    /// <summary>The distance between the query vector and each match, in the metric of the index.</summary>
    Distance = 1,

    /// <summary>The metadata stored with each match.</summary>
    Metadata = 2,
}
