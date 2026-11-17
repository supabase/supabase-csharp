using System.Collections.Generic;

namespace Supabase.Postgrest.SingleOrThrow;

internal sealed record RowRepresentation
{
    private const string ArrayType = "application/vnd.pgrst.array+json";
    private const string ObjectType = "application/vnd.pgrst.object+json";
    private const string NullsStripped = ";nulls=stripped";

    private readonly bool singleObject;
    private readonly bool nullsStripped;

    private RowRepresentation(bool singleObject, bool nullsStripped)
    {
        this.singleObject = singleObject;
        this.nullsStripped = nullsStripped;
    }

    internal static RowRepresentation Default { get; } = new(false, false);

    internal RowRepresentation AsSingleObject() => new(true, this.nullsStripped);

    internal RowRepresentation WithNullsStripped() => new(this.singleObject, true);

    internal void ApplyTo(Dictionary<string, string> headers)
    {
        if (this.singleObject || this.nullsStripped)
            headers.TryAdd("Accept", this.MediaType);
    }

    private string MediaType => $"{(this.singleObject ? ObjectType : ArrayType)}{(this.nullsStripped ? NullsStripped : string.Empty)}";
}
