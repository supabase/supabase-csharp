using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;

namespace Storage.Tests.DeleteVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).DeleteVectorsAsync</c> against the local stack's
/// vector service. Each test gets its own bucket and index, seeded and checked over plain HTTP because creating
/// a bucket and reading or writing vectors are not in the SDK yet, and torn down whatever the outcome.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorDeletionTests
{
    private const string IndexName = "docs-v1";
    private static readonly string[] SeededKeys = { "doc-1", "doc-2", "doc-3" };
    private readonly Client storage = Helpers.GetServiceClient();
    private readonly HttpClient http = new();
    private readonly string bucketName = $"e2e-{Guid.NewGuid():N}";

    [TestInitialize]
    public async Task SeedIndex()
    {
        await this.PostToVectorService("CreateVectorBucket", new { vectorBucketName = this.bucketName });
        await this.storage.Vectors.From(this.bucketName).CreateIndexAsync(new NewVectorIndex
        {
            IndexName = IndexName,
            Dimension = 2,
            DistanceMetric = VectorDistanceMetric.Cosine,
        });
        await this.PostToVectorService("PutVectors", new
        {
            vectorBucketName = this.bucketName,
            indexName = IndexName,
            vectors = SeededKeys.Select(key => new { key, data = new { float32 = new[] { 0.5f, 0.25f } } }),
        });
    }

    [TestCleanup]
    public async Task RemoveIndex()
    {
        await IgnoreRefusal(() => this.storage.Vectors.From(this.bucketName).DeleteIndexAsync(IndexName));
        await IgnoreRefusal(() => this.storage.Vectors.DeleteVectorBucketAsync(this.bucketName));
        this.http.Dispose();
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldRemoveOnlyTheVectorsUnderTheGivenKeys()
    {
        await this.Index().DeleteVectorsAsync(new[] { "doc-1", "doc-2" });
        (await this.StoredKeys()).Should().Equal(new[] { "doc-3" }, "only the named vectors are deleted");
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldIgnoreAKeyThatMatchesNoVector()
    {
        await this.Index().DeleteVectorsAsync(new[] { "missing" });
        (await this.StoredKeys()).Should().BeEquivalentTo(SeededKeys, "deleting an unknown key is a no-op for the service");
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.storage.Vectors.From(this.bucketName).Index("missing-index").DeleteVectorsAsync(SeededKeys))
            .Should().ThrowAsync<SupabaseStorageException>("the index does not exist");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
        }
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldSurfaceTheServiceRejection_GivenAnEmptyCollection()
    {
        var thrown = await FluentActions.Awaiting(() => this.Index().DeleteVectorsAsync(Array.Empty<string>()))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the batch-size rule and requires at least one key");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
        }
    }

    private IVectorIndex Index() => this.storage.Vectors.From(this.bucketName).Index(IndexName);

    private async Task<IReadOnlyList<string>> StoredKeys()
    {
        var body = await this.PostToVectorService("GetVectors", new { vectorBucketName = this.bucketName, indexName = IndexName, keys = SeededKeys });
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("vectors").EnumerateArray()
            .Select(vector => vector.GetProperty("key").GetString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string> PostToVectorService(string operation, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Helpers.StorageUrl}/vector/{operation}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
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
