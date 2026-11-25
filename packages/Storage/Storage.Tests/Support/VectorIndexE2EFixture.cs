using System;
using System.Collections.Generic;
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
using Supabase.Storage.PutVectors;

namespace Storage.Tests.Support;

/// <summary>
///     Base fixture for E2E tests of the operations scoped to one vector index. Each test gets its own uniquely named
///     vector bucket holding one two-dimensional index, removed whatever the outcome.
/// </summary>
public abstract class VectorIndexE2EFixture
{
    protected const string IndexName = "docs-v1";

    private static readonly JsonSerializerOptions SeedOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly HttpClient http = new();
    private readonly List<string> trackedIndexNames = new() { IndexName };

    /// <summary>Service-role client: the vector service accepts no other role.</summary>
    protected Client Storage { get; } = Helpers.GetServiceClient();

    /// <summary>The name of this test's vector bucket.</summary>
    protected string VectorBucketName { get; } = $"e2e-{Guid.NewGuid():N}";

    /// <summary>The index under test.</summary>
    protected IVectorIndex Index => this.Storage.Vectors.From(this.VectorBucketName).Index(IndexName);

    [TestInitialize]
    public async Task CreateBucketWithIndex()
    {
        await this.Storage.Vectors.CreateVectorBucketAsync(this.VectorBucketName);
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
        foreach (var indexName in this.trackedIndexNames)
        {
            await IgnoreRefusal(() => this.Storage.Vectors.From(this.VectorBucketName).DeleteIndexAsync(indexName));
        }
        await IgnoreRefusal(() => this.Storage.Vectors.DeleteVectorBucketAsync(this.VectorBucketName));
        this.http.Dispose();
    }

    /// <summary>Registers an index a test creates in its bucket, so teardown removes it before the bucket. Returns the name for chaining.</summary>
    protected string TrackIndex(string indexName)
    {
        this.trackedIndexNames.Add(indexName);
        return indexName;
    }

    /// <summary>Writes the vectors to the index.</summary>
    protected Task PutVectors(params SeededVector[] vectors) =>
        this.Index.PutVectorsAsync(vectors.Select(ToNewVector).ToArray());

    /// <summary>Posts the body to a vector service route as the service role and returns the response body, so a test can check the stored state independently of the SDK; fails the test on refusal.</summary>
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

    private static NewVector ToNewVector(SeededVector vector) => new()
    {
        Key = vector.Key,
        Data = vector.Data,
        Metadata = vector.Metadata?.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value)),
    };

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
