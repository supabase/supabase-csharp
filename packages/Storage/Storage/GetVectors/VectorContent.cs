using System;

namespace Supabase.Storage.GetVectors;

/// <summary>
/// What the service returns with each vector besides its key. Combine the values to ask for several,
/// such as <c>VectorContent.Data | VectorContent.Metadata</c>.
/// </summary>
[Flags]
public enum VectorContent
{
    /// <summary>Nothing beyond the key is asked for; the service applies its own default.</summary>
    None = 0,

    /// <summary>The components of each vector.</summary>
    Data = 1,

    /// <summary>The metadata stored with each vector.</summary>
    Metadata = 2,
}
