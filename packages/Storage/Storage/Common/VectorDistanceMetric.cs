namespace Supabase.Storage.Common;

/// <summary>
/// The measure an index uses to rank vectors by similarity. A metric the service adds after this SDK version still
/// decodes: it equals none of the known metrics and <see cref="ToString"/> returns the name the service sent.
/// </summary>
public sealed record VectorDistanceMetric
{
    private readonly string name;

    private VectorDistanceMetric(string name) => this.name = name;

    /// <summary>Cosine distance.</summary>
    public static VectorDistanceMetric Cosine { get; } = new("cosine");

    /// <summary>Euclidean distance.</summary>
    public static VectorDistanceMetric Euclidean { get; } = new("euclidean");

    /// <summary>The name of the metric, as the service knows it.</summary>
    /// <returns>The metric name, such as <c>cosine</c>.</returns>
    public override string ToString() => this.name;

    internal static VectorDistanceMetric FromWire(string name) => new(name);
}
