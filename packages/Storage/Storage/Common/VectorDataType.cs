namespace Supabase.Storage.Common;

/// <summary>
/// The numeric type of each component of the vectors an index stores. A type the service adds after this SDK version
/// still decodes: it equals none of the known types and <see cref="ToString"/> returns the name the service sent.
/// </summary>
public sealed record VectorDataType
{
    private readonly string name;

    private VectorDataType(string name) => this.name = name;

    /// <summary>32-bit floating point components.</summary>
    public static VectorDataType Float32 { get; } = new("float32");

    /// <summary>The name of the data type, as the service knows it.</summary>
    /// <returns>The data type name, such as <c>float32</c>.</returns>
    public override string ToString() => this.name;

    internal static VectorDataType FromWire(string name) => new(name);
}
