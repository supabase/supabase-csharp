using System;
using System.Text.Json.Serialization;

namespace Supabase.Storage.AnalyticsBuckets.CreateBucket;

internal sealed class CreateAnalyticsBucketBody
{
    private CreateAnalyticsBucketBody(string name) => this.Name = name;

    [JsonPropertyName("name")]
    public string Name { get; }

    internal static CreateAnalyticsBucketBody For(string name)
    {
        RejectBlank(name);
        return new CreateAnalyticsBucketBody(name);
    }

    private static void RejectBlank(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An analytics bucket name must be provided.", nameof(name));
    }
}
