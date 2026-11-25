using System.Text.Json.Serialization;

namespace Supabase.Storage.ListVectorBuckets;

internal sealed class ListVectorBucketsBody
{
    internal ListVectorBucketsBody(ListVectorBucketsOptions options)
    {
        this.MaxResults = options.MaxResults;
        this.NextToken = options.NextToken;
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
