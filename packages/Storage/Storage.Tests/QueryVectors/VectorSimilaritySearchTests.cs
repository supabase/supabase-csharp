using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.QueryVectors;

namespace Storage.Tests.QueryVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).QueryVectorsAsync</c> against the local stack's vector
/// service, on a cosine index seeded over plain HTTP with three vectors at known angles from the query vector.
/// The components are exact in half precision, which the local pgvector store uses.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorSimilaritySearchTests : VectorIndexE2EFixture
{
    private static readonly float[] Query = { 1f, 0f };

    [TestInitialize]
    public Task SeedVectors() =>
        this.PutVectors(
            new SeededVector("same", new[] { 1f, 0f }, new Dictionary<string, object> { ["kind"] = "x", ["rank"] = 1 }),
            new SeededVector("diagonal", new[] { 1f, 1f }, new Dictionary<string, object> { ["kind"] = "x", ["rank"] = 2 }),
            new SeededVector("orthogonal", new[] { 0f, 1f }, new Dictionary<string, object> { ["kind"] = "y", ["rank"] = 3 }));

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnTheNearestVectorsFirst()
    {
        var result = await this.Index.QueryVectorsAsync(Query, 3);
        using (new AssertionScope())
        {
            result.Matches.Select(match => match.Key).Should().Equal("same", "diagonal", "orthogonal");
            result.DistanceMetric.Should().Be(VectorDistanceMetric.Cosine);
            result.Matches.Should().OnlyContain(match => match.Distance == null && match.Metadata == null, "the service returns neither unless asked");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnOnlyTheClosest_GivenATopKBelowTheIndexSize()
    {
        (await this.Index.QueryVectorsAsync(Query, 2)).Matches.Select(match => match.Key)
            .Should().Equal(new[] { "same", "diagonal" });
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnTheDistances_GivenDistanceContent()
    {
        var result = await this.Index.QueryVectorsAsync(Query, 3, new QueryVectorsOptions { Include = VectorMatchContent.Distance });
        result.Matches.Select(match => match.Distance!.Value).Should().BeEquivalentTo(new[] { 0f, 0.2929f, 1f },
            options => options.Using<float>(context => context.Subject.Should().BeApproximately(context.Expectation, 0.01f)).WhenTypeIs<float>().WithStrictOrdering(),
            "cosine distance is zero for the same direction, one for orthogonal vectors");
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnTheMetadata_GivenMetadataContent()
    {
        var result = await this.Index.QueryVectorsAsync(Query, 1, new QueryVectorsOptions { Include = VectorMatchContent.Metadata });
        result.Matches.Single().Metadata!["kind"].GetString().Should().Be("x");
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnOnlyMatchingVectors_GivenAFilter()
    {
        var options = new QueryVectorsOptions { Filter = JsonDocument.Parse("{\"kind\":\"y\"}").RootElement };
        (await this.Index.QueryVectorsAsync(Query, 3, options)).Matches.Select(match => match.Key)
            .Should().Equal(new[] { "orthogonal" }, "only the vectors whose metadata satisfies the filter can match");
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).Index("missing-index").QueryVectorsAsync(Query, 3))
            .Should().ThrowAsync<SupabaseStorageException>("the index does not exist");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldSurfaceTheServiceRejection_GivenATopKOfZero()
    {
        var thrown = await FluentActions.Awaiting(() => this.Index.QueryVectorsAsync(Query, 0))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the topK range");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
        }
    }
}
