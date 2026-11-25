using System.Collections.Generic;

namespace Supabase.Storage.Common;

/// <summary>
/// The components of a vector, tagged with their numeric type. A type the service adds after this SDK version still
/// decodes: <see cref="DataType"/> keeps the name the service sent and the components of known types are null.
/// </summary>
public sealed record VectorData
{
    /// <summary>The numeric type of the components.</summary>
    public required VectorDataType DataType { get; init; }

    /// <summary>The components when <see cref="DataType"/> is <see cref="VectorDataType.Float32"/>, otherwise null.</summary>
    public IReadOnlyList<float>? Float32 { get; init; }
}
