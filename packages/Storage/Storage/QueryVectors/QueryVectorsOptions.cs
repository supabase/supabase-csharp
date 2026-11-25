using System.Text.Json;

namespace Supabase.Storage.QueryVectors;

/// <summary>
/// Filter, content and paging options for a similarity search. An option left unset is not sent, and the service
/// applies its default.
/// </summary>
public sealed record QueryVectorsOptions
{
    /// <summary>
    /// A metadata filter as the service defines it, such as <c>{"category":"docs"}</c> or
    /// <c>{"$and":[{"year":{"$gte":2024}},{"lang":{"$in":["en","fr"]}}]}</c>. The service validates it.
    /// </summary>
    public JsonElement? Filter { get; init; }

    /// <summary>What to return with each match. With <see cref="VectorMatchContent.None"/>, the service applies its own default.</summary>
    public VectorMatchContent Include { get; init; } = VectorMatchContent.None;

    /// <summary>
    /// The <see cref="VectorQueryResult.NextToken"/> of the previous result, to read the matches after it. Only
    /// services backed by S3 Vectors paginate a query; the local stack rejects it.
    /// </summary>
    public string? NextToken { get; init; }
}
