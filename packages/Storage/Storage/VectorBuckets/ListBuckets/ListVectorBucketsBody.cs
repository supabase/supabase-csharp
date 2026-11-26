using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.ListBuckets;

internal sealed class ListVectorBucketsBody
{
    internal ListVectorBucketsBody(ListVectorBucketsOptions options)
    {
        this.MaxResults = options.PageSize;
        this.NextToken = options.NextPageToken;
        this.Prefix = options.Prefix;
    }

    [JsonPropertyName("maxResults")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxResults { get; }

    [JsonPropertyName("nextToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextToken { get; }

    [JsonPropertyName("prefix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefix { get; }
}
