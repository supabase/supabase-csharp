using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.DeleteVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).DeleteVectorsAsync</c> against the local stack's
/// vector service. The remaining vectors are read over plain HTTP, through the service's <c>GetVectors</c> route,
/// so the assertion does not depend on the SDK's own retrieval.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorDeletionTests : VectorIndexE2EFixture
{
    private static readonly string[] SeededKeys = { "doc-1", "doc-2", "doc-3" };

    [TestInitialize]
    public Task SeedVectors() =>
        this.PutVectors(SeededKeys.Select(key => new SeededVector(key, new[] { 0.5f, 0.25f })).ToArray());

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldRemoveOnlyTheVectorsUnderTheGivenKeys()
    {
        await this.Index.DeleteVectorsAsync(new[] { "doc-1", "doc-2" });
        (await this.StoredKeys()).Should().Equal(new[] { "doc-3" }, "only the named vectors are deleted");
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldIgnoreAKeyThatMatchesNoVector()
    {
        await this.Index.DeleteVectorsAsync(new[] { "missing" });
        (await this.StoredKeys()).Should().BeEquivalentTo(SeededKeys, "deleting an unknown key is a no-op for the service");
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).Index("missing-index").DeleteVectorsAsync(SeededKeys))
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
        var thrown = await FluentActions.Awaiting(() => this.Index.DeleteVectorsAsync(Array.Empty<string>()))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the batch-size rule and requires at least one key");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
        }
    }

    private async Task<IReadOnlyList<string>> StoredKeys()
    {
        var body = await this.PostToVectorService("GetVectors", new { vectorBucketName = this.VectorBucketName, indexName = IndexName, keys = SeededKeys });
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("vectors").EnumerateArray()
            .Select(vector => vector.GetProperty("key").GetString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }
}
