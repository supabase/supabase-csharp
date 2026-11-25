using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;

namespace Storage.Tests.Support;

/// <summary>
///     Base fixture for E2E tests of the operations scoped to one vector index. Each test gets its own uniquely named
///     vector bucket holding one two-dimensional index, removed whatever the outcome. The bucket and the vectors are
///     written over plain HTTP, through the service's own routes, because the SDK cannot create a vector bucket or put
///     vectors yet.
/// </summary>
public abstract class VectorIndexE2EFixture
{
    protected const string IndexName = "docs-v1";

    private static readonly JsonSerializerOptions SeedOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly HttpClient http = new();

    /// <summary>Service-role client: the vector service accepts no other role.</summary>
    protected Client Storage { get; } = Helpers.GetServiceClient();

    /// <summary>The name of this test's vector bucket.</summary>
    protected string VectorBucketName { get; } = $"e2e-{Guid.NewGuid():N}";

    /// <summary>The index under test.</summary>
    protected IVectorIndex Index => this.Storage.Vectors.From(this.VectorBucketName).Index(IndexName);

    [TestInitialize]
    public async Task CreateBucketWithIndex()
    {
        await this.PostToVectorService("CreateVectorBucket", new { vectorBucketName = this.VectorBucketName });
        await this.Storage.Vectors.From(this.VectorBucketName).CreateIndexAsync(new NewVectorIndex
        {
            IndexName = IndexName,
            Dimension = 2,
            DistanceMetric = VectorDistanceMetric.Cosine,
        });
    }

    [TestCleanup]
    public async Task RemoveBucketWithIndex()
    {
        await IgnoreRefusal(() => this.Storage.Vectors.From(this.VectorBucketName).DeleteIndexAsync(IndexName));
        await IgnoreRefusal(() => this.Storage.Vectors.DeleteVectorBucketAsync(this.VectorBucketName));
        this.http.Dispose();
    }

    /// <summary>Writes the vectors to the index through the service's <c>PutVectors</c> route.</summary>
    protected Task PutVectors(params SeededVector[] vectors) =>
        this.PostToVectorService("PutVectors", new
        {
            vectorBucketName = this.VectorBucketName,
            indexName = IndexName,
            vectors = vectors.Select(vector => new { key = vector.Key, data = new { float32 = vector.Data }, metadata = vector.Metadata }),
        });

    /// <summary>Posts the body to a vector service route as the service role and returns the response body; fails the test on refusal.</summary>
    protected async Task<string> PostToVectorService(string operation, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Helpers.StorageUrl}/vector/{operation}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, SeedOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Helpers.ServiceKey);
        using var response = await this.http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"seeding through {operation} must succeed: {content}");
        return content;
    }

    private static async Task IgnoreRefusal(Func<Task> teardown)
    {
        try
        {
            await teardown();
        }
        catch (SupabaseStorageException)
        {
        }
    }
}
