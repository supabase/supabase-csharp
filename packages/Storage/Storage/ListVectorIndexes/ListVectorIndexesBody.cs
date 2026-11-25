using System.Text.Json.Serialization;

namespace Supabase.Storage.ListVectorIndexes;

internal sealed class ListVectorIndexesBody
{
    internal ListVectorIndexesBody(string vectorBucketName, ListVectorIndexesOptions options)
    {
        this.VectorBucketName = vectorBucketName;
        this.MaxResults = options.MaxResults;
        this.NextToken = options.NextToken;
        this.Prefix = options.Prefix;
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

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
