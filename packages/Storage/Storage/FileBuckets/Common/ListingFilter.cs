using System.Text.Json.Serialization;

namespace Supabase.Storage.FileBuckets.Common;

/// <summary>
/// Whether a listing leaves out, includes or returns only a kind of entry.
/// </summary>
[JsonConverter(typeof(ListingFilterConverter))]
public enum ListingFilter
{
    /// <summary>The entries are left out.</summary>
    Exclude,

    /// <summary>The entries are listed with the others.</summary>
    Include,

    /// <summary>Only these entries are listed.</summary>
    Only,
}
