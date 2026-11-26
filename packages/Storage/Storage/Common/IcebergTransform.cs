using System;

namespace Supabase.Storage.Common;

/// <summary>
/// The transform that turns a column's values into partition values. A transform the service adds after this SDK
/// version still decodes: it equals none of the known transforms and <see cref="ToString"/> returns the name the
/// service sent.
/// </summary>
public sealed record IcebergTransform
{
    private readonly string name;

    private IcebergTransform(string name) => this.name = name;

    /// <summary>The value itself.</summary>
    public static IcebergTransform Identity { get; } = new("identity");

    /// <summary>The year of a date or timestamp.</summary>
    public static IcebergTransform Year { get; } = new("year");

    /// <summary>The month of a date or timestamp.</summary>
    public static IcebergTransform Month { get; } = new("month");

    /// <summary>The day of a date or timestamp.</summary>
    public static IcebergTransform Day { get; } = new("day");

    /// <summary>The hour of a timestamp.</summary>
    public static IcebergTransform Hour { get; } = new("hour");

    /// <summary>Always null: drops the partition field without removing it.</summary>
    public static IcebergTransform Void { get; } = new("void");

    /// <summary>A hash of the value, modulo a number of buckets.</summary>
    /// <param name="count">The number of buckets. The service validates it.</param>
    /// <returns>The bucket transform, such as <c>bucket[16]</c>.</returns>
    public static IcebergTransform Bucket(int count) => new(FormattableString.Invariant($"bucket[{count}]"));

    /// <summary>The value truncated to a width.</summary>
    /// <param name="width">The width to truncate to. The service validates it.</param>
    /// <returns>The truncate transform, such as <c>truncate[10]</c>.</returns>
    public static IcebergTransform Truncate(int width) => new(FormattableString.Invariant($"truncate[{width}]"));

    /// <summary>The name of the transform, as the service knows it.</summary>
    /// <returns>The transform name, such as <c>day</c> or <c>bucket[16]</c>.</returns>
    public override string ToString() => this.name;

    internal static IcebergTransform FromWire(string name) => new(name);
}
