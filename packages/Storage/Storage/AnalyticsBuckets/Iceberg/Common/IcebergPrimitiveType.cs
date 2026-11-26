using System;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// A primitive Iceberg type. A type the service adds after this SDK version still decodes: it equals none of the known
/// types and <see cref="ToString"/> returns the name the service sent.
/// </summary>
public sealed record IcebergPrimitiveType : IcebergType
{
    private readonly string name;

    private IcebergPrimitiveType(string name) => this.name = name;

    /// <summary>True or false.</summary>
    public static IcebergPrimitiveType Boolean { get; } = new("boolean");

    /// <summary>32-bit signed integer.</summary>
    public static IcebergPrimitiveType Int { get; } = new("int");

    /// <summary>64-bit signed integer.</summary>
    public static IcebergPrimitiveType Long { get; } = new("long");

    /// <summary>32-bit floating point number.</summary>
    public static IcebergPrimitiveType Float { get; } = new("float");

    /// <summary>64-bit floating point number.</summary>
    public static IcebergPrimitiveType Double { get; } = new("double");

    /// <summary>Calendar date without time or time zone.</summary>
    public static IcebergPrimitiveType Date { get; } = new("date");

    /// <summary>Time of day, in microseconds, without date or time zone.</summary>
    public static IcebergPrimitiveType Time { get; } = new("time");

    /// <summary>Timestamp, in microseconds, without time zone.</summary>
    public static IcebergPrimitiveType Timestamp { get; } = new("timestamp");

    /// <summary>Timestamp, in microseconds, stored in UTC.</summary>
    public static IcebergPrimitiveType TimestampTz { get; } = new("timestamptz");

    /// <summary>UTF-8 character string.</summary>
    public static IcebergPrimitiveType String { get; } = new("string");

    /// <summary>Universally unique identifier.</summary>
    public static IcebergPrimitiveType Uuid { get; } = new("uuid");

    /// <summary>Byte array of any length.</summary>
    public static IcebergPrimitiveType Binary { get; } = new("binary");

    /// <summary>Fixed-point decimal number.</summary>
    /// <param name="precision">The number of digits. The service validates it.</param>
    /// <param name="scale">The number of digits after the decimal point. The service validates it.</param>
    /// <returns>The decimal type, such as <c>decimal(10,2)</c>.</returns>
    public static IcebergPrimitiveType Decimal(int precision, int scale) =>
        new(FormattableString.Invariant($"decimal({precision},{scale})"));

    /// <summary>Byte array of a fixed length.</summary>
    /// <param name="length">The number of bytes. The service validates it.</param>
    /// <returns>The fixed type, such as <c>fixed[16]</c>.</returns>
    public static IcebergPrimitiveType Fixed(int length) =>
        new(FormattableString.Invariant($"fixed[{length}]"));

    /// <summary>The name of the type, as the service knows it.</summary>
    /// <returns>The type name, such as <c>long</c> or <c>decimal(10,2)</c>.</returns>
    public override string ToString() => this.name;

    internal static IcebergPrimitiveType FromWire(string name) => new(name);
}
