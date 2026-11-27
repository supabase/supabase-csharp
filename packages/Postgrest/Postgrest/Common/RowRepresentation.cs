using System.Collections.Generic;

namespace Supabase.Postgrest.Common;

internal sealed record RowRepresentation
{
    private const string ArrayType = "application/vnd.pgrst.array+json";
    private const string ObjectType = "application/vnd.pgrst.object+json";
    private const string CsvType = "text/csv";
    private const string NullsStripped = ";nulls=stripped";

    private readonly Shape shape;
    private readonly bool nullsStripped;

    private RowRepresentation(Shape shape, bool nullsStripped)
    {
        this.shape = shape;
        this.nullsStripped = nullsStripped;
    }

    private enum Shape
    {
        Array,
        SingleObject,
        Csv
    }

    internal static RowRepresentation Default { get; } = new(Shape.Array, false);

    internal RowRepresentation AsSingleObject() => new(Shape.SingleObject, this.nullsStripped);

    internal RowRepresentation AsCsv() => new(Shape.Csv, this.nullsStripped);

    internal RowRepresentation WithNullsStripped() => new(this.shape, true);

    internal void ApplyTo(Dictionary<string, string> headers)
    {
        if (this.MediaType is { } mediaType)
            headers.TryAdd("Accept", mediaType);
    }

    private string? MediaType => this.shape switch
    {
        Shape.Csv => CsvType,
        Shape.SingleObject => ObjectType + this.NullsParameter,
        _ when this.nullsStripped => ArrayType + NullsStripped,
        _ => null
    };

    private string NullsParameter => this.nullsStripped ? NullsStripped : string.Empty;
}
