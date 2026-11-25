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
using Supabase.Storage.PutVectors;

namespace Storage.Tests.PutVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).PutVectorsAsync</c> against the local stack's vector
/// service. What was stored is read back over plain HTTP, through the service's <c>GetVectors</c> route, so the
/// assertions do not depend on the SDK's own retrieval. The components are exact in half precision, which the local
/// pgvector store uses.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorStorageTests : VectorIndexE2EFixture
{
    [TestMethod]
    public async Task PutVectorsAsync_ShouldStoreTheVectorsWithTheirMetadata()
    {
        var metadata = new Dictionary<string, JsonElement> { ["title"] = JsonSerializer.SerializeToElement("Intro") };
        await this.Index.PutVectorsAsync(new[]
        {
            new NewVector { Key = "doc-1", Data = new[] { 0.5f, 0.25f }, Metadata = metadata },
            new NewVector { Key = "doc-2", Data = new[] { 1f, 2f } },
        });
        var stored = await this.Stored("doc-1", "doc-2");
        using (new AssertionScope())
        {
            stored["doc-1"].GetProperty("data").GetProperty("float32").EnumerateArray().Select(value => value.GetSingle()).Should().Equal(0.5f, 0.25f);
            stored["doc-1"].GetProperty("metadata").GetProperty("title").GetString().Should().Be("Intro");
            stored["doc-2"].GetProperty("data").GetProperty("float32").EnumerateArray().Select(value => value.GetSingle()).Should().Equal(1f, 2f);
        }
    }

    [TestMethod]
    public async Task PutVectorsAsync_ShouldReplaceTheVector_GivenAKeyAlreadyInUse()
    {
        await this.Index.PutVectorsAsync(new[] { new NewVector { Key = "doc-1", Data = new[] { 0.5f, 0.25f } } });
        await this.Index.PutVectorsAsync(new[] { new NewVector { Key = "doc-1", Data = new[] { 2f, 1f } } });
        (await this.Stored("doc-1"))["doc-1"].GetProperty("data").GetProperty("float32").EnumerateArray().Select(value => value.GetSingle())
            .Should().Equal(new[] { 2f, 1f }, "storing under a used key replaces the vector");
    }

    [TestMethod]
    public async Task PutVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).Index("missing-index")
                .PutVectorsAsync(new[] { new NewVector { Key = "doc-1", Data = new[] { 1f, 2f } } }))
            .Should().ThrowAsync<SupabaseStorageException>("the index does not exist");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
        }
    }

    [TestMethod]
    public async Task PutVectorsAsync_ShouldSurfaceTheServiceRejection_GivenAnEmptyCollection()
    {
        var thrown = await FluentActions.Awaiting(() => this.Index.PutVectorsAsync(Array.Empty<NewVector>()))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the batch-size rule and requires at least one vector");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
        }
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> Stored(params string[] keys)
    {
        var body = await this.PostToVectorService("GetVectors", new { vectorBucketName = this.VectorBucketName, indexName = IndexName, keys, returnData = true, returnMetadata = true });
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("vectors").EnumerateArray()
            .ToDictionary(vector => vector.GetProperty("key").GetString()!, vector => vector.Clone());
    }
}
