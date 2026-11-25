using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;
using Supabase.Storage.GetVectors;

namespace Storage.Tests.GetVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).GetVectorsAsync</c> against the local stack's vector
/// service: what each <see cref="VectorContent"/> choice returns, and how a key that matches nothing is handled.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorRetrievalTests : VectorIndexE2EFixture
{
    private static readonly string[] SeededKeys = { "doc-1", "doc-2" };

    [TestInitialize]
    public Task SeedVectors() =>
        this.PutVectors(
            new SeededVector("doc-1", new[] { 0.5f, 0.25f }, new Dictionary<string, object> { ["title"] = "Intro", ["page"] = 3 }),
            new SeededVector("doc-2", new[] { 1f, 2f }));

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnOnlyTheKeys_GivenNoContent()
    {
        var vectors = await this.Index.GetVectorsAsync(SeededKeys);
        vectors.Should().BeEquivalentTo(new[] { new StoredVector { Key = "doc-1" }, new StoredVector { Key = "doc-2" } },
            "the service returns neither data nor metadata unless asked");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnTheData_GivenDataContent()
    {
        var vectors = await this.Index.GetVectorsAsync(SeededKeys, VectorContent.Data);
        vectors.Single(vector => vector.Key == "doc-1").Data.Should().Equal(0.5f, 0.25f);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnTheMetadata_GivenMetadataContent()
    {
        var vectors = await this.Index.GetVectorsAsync(SeededKeys, VectorContent.Metadata);
        var withMetadata = vectors.Single(vector => vector.Key == "doc-1");
        using (new AssertionScope())
        {
            withMetadata.Metadata!["title"].GetString().Should().Be("Intro");
            withMetadata.Metadata["page"].GetInt32().Should().Be(3);
            withMetadata.Data.Should().BeNull("data was not asked for");
            vectors.Single(vector => vector.Key == "doc-2").Metadata.Should().BeEmpty("a vector stored without metadata has none");
        }
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldLeaveOutAKeyThatMatchesNoVector()
    {
        var vectors = await this.Index.GetVectorsAsync(new[] { "doc-1", "missing" });
        vectors.Select(vector => vector.Key).Should().Equal(new[] { "doc-1" }, "the service returns only the vectors it found");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).Index("missing-index").GetVectorsAsync(SeededKeys))
            .Should().ThrowAsync<SupabaseStorageException>("the index does not exist");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
        }
    }
}
