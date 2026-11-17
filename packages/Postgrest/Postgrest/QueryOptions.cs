using System.Collections.Generic;
using Supabase.Core.Attributes;
using Supabase.Postgrest.Extensions;
#pragma warning disable CS1591
namespace Supabase.Postgrest;


public class QueryOptions
{
    public enum ReturnType
    {
        [MapTo("minimal")]
        Minimal,
        [MapTo("representation")]
        Representation
    };

    public enum CountType
    {
        [MapTo("none")]
        None,
        [MapTo("exact")]
        Exact,
        [MapTo("planned")]
        Planned,
        [MapTo("estimated")]
        Estimated
    };

    public enum DuplicateResolutionType
    {
        [MapTo("merge-duplicates")]
        MergeDuplicates,
        [MapTo("ignore-duplicates")]
        IgnoreDuplicates
    }

    public enum MissingFieldType
    {
        [MapTo("null")]
        Null,
        [MapTo("default")]
        ColumnDefault
    }

    /// <summary>
    /// By default the new record is returned. Set this to 'Minimal' if you don't need this value.
    /// </summary>
    [ImplementedCapability("database.mutate.select_after_mutation", "Mutations return the affected rows by default (QueryOptions.Returning = Representation).")]
    public ReturnType Returning { get; set; } = ReturnType.Representation;

    /// <summary>
    /// Specifies if duplicate rows should be ignored and not inserted.
    /// </summary>
    public DuplicateResolutionType DuplicateResolution { get; set; } = DuplicateResolutionType.MergeDuplicates;

    /// <summary>
    /// Count algorithm to use to count rows in a table.
    /// </summary>
    [ImplementedCapability("database.mutate.count", "Applied to Insert/Upsert/Update/Delete; the count is read back from ModeledResponse.Count.")]
    public CountType Count { get; set; } = CountType.None;

    /// <summary>
    /// What a bulk insert or upsert writes for a field that some rows omit: SQL NULL (default), or the column's
    /// database DEFAULT with <see cref="MissingFieldType.ColumnDefault"/>. An explicitly supplied null stays null.
    /// Only affects bulk writes, which name the union of the rows' fields in the columns parameter.
    /// </summary>
    /// <example>
    /// <code>
    /// await table.Insert(rows, new QueryOptions { MissingFields = QueryOptions.MissingFieldType.ColumnDefault });
    /// </code>
    /// </example>
    [ImplementedCapability("database.mutate.missing_defaults", "Applied to bulk Insert/Upsert; sends Prefer: missing=default.")]
    public MissingFieldType MissingFields { get; set; } = MissingFieldType.Null;

    /// <summary>
    /// If the record should be upserted
    /// </summary>
    public bool Upsert { get; set; }

    /// <summary>
    /// /// By specifying the onConflict query parameter, you can make UPSERT work on a column(s) that has a UNIQUE constraint.
    /// </summary>
    public string? OnConflict { get; set; }

    internal QueryOptions Clone() => (QueryOptions) this.MemberwiseClone();

    public Dictionary<string, string> ToHeaders()
    {
        var headers = new Dictionary<string, string>();
        var prefersHeaders = new List<string>();

        if (this.Upsert)
        {
            var resolverAttr = this.DuplicateResolution.GetAttribute<MapToAttribute>();
            prefersHeaders.Add($"resolution={resolverAttr?.Mapping}");
        }

        var returnAttr = this.Returning.GetAttribute<MapToAttribute>();
        if (returnAttr != null)
        {
            prefersHeaders.Add($"return={returnAttr.Mapping}");
        }

        var countAttr = this.Count.GetAttribute<MapToAttribute>();
        if (this.Count != CountType.None && countAttr != null)
        {
            prefersHeaders.Add($"count={countAttr.Mapping}");
        }

        if (this.MissingFields == MissingFieldType.ColumnDefault)
        {
            prefersHeaders.Add($"missing={this.MissingFields.GetAttribute<MapToAttribute>()?.Mapping}");
        }

        headers.Add("Prefer", string.Join(",", prefersHeaders.ToArray()));

        if (this.Returning == ReturnType.Minimal)
        {
            headers.Add("Accept", "*/*");
        }

        return headers;
    }
}
