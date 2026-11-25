using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.ListVectors;

namespace Storage.Tests.ListVectors;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).Index(name).ListVectorsAsync</c> against the local stack's vector
/// service: paging with the next token, what each <see cref="VectorContent"/> choice returns, and segments.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorListingTests : VectorIndexE2EFixture
{
    private static readonly string[] SeededKeys = { "doc-1", "doc-2", "doc-3" };

    [TestInitialize]
    public Task SeedVectors() =>
        this.PutVectors(
            new SeededVector("doc-1", new[] { 0.5f, 0.25f }, new Dictionary<string, object> { ["title"] = "Intro" }),
            new SeededVector("doc-2", new[] { 1f, 2f }),
            new SeededVector("doc-3", new[] { 2f, 1f }));

    [TestMethod]
    public async Task ListVectorsAsync_ShouldReturnEveryVectorOnOnePage_GivenNoOptions()
    {
        var page = await this.Index.ListVectorsAsync();
        using (new AssertionScope())
        {
            page.Items.Should().BeEquivalentTo(SeededKeys.Select(key => new StoredVector { Key = key }), "the service returns keys only unless asked");
            page.NextToken.Should().BeNull("every vector fits on the default page");
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldPageThroughTheIndex_GivenMaxResults()
    {
        var options = new ListVectorsOptions { MaxResults = 2 };
        var first = await this.Index.ListVectorsAsync(options);
        var second = await this.Index.ListVectorsAsync(options with { NextToken = first.NextToken });
        using (new AssertionScope())
        {
            first.Items.Should().HaveCount(2);
            second.NextToken.Should().BeNull("the second page is the last");
            first.Items.Concat(second.Items).Select(vector => vector.Key).Should().BeEquivalentTo(SeededKeys, "the pages together cover the index");
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldReturnDataAndMetadata_GivenBothContent()
    {
        var page = await this.Index.ListVectorsAsync(new ListVectorsOptions { Include = VectorContent.Data | VectorContent.Metadata });
        var vector = page.Items.Single(item => item.Key == "doc-1");
        using (new AssertionScope())
        {
            vector.Data.Should().Equal(0.5f, 0.25f);
            vector.Metadata!["title"].GetString().Should().Be("Intro");
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldSplitTheIndexIntoDisjointSegments()
    {
        var firstHalf = await this.Index.ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(0, 2) });
        var secondHalf = await this.Index.ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(1, 2) });
        firstHalf.Items.Concat(secondHalf.Items).Select(vector => vector.Key).Should().BeEquivalentTo(SeededKeys,
            "every vector belongs to exactly one segment");
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldThrowInvalidParameter_GivenASegmentIndexNotBelowTheCount()
    {
        var thrown = await FluentActions.Awaiting(() => this.Index.ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(2, 2) }))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the segment range rule");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidParameter");
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).Index("missing-index").ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>("the index does not exist");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
        }
    }
}
