using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.FileBuckets.Common;

internal sealed class ListingFilterConverter : JsonStringEnumConverter<ListingFilter>
{
    public ListingFilterConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}
