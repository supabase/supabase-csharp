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
/// End-to-end tests for scanning a whole vector index with parallel workers, each reading its own segment through
/// <c>Vectors.From(bucket).Index(name).ListVectorsAsync</c> against the local stack's vector service.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ParallelVectorScanTests : VectorIndexE2EFixture
{
    private const int SegmentCount = 4;
    private static readonly string[] SeededKeys = { "doc-1", "doc-2", "doc-3", "doc-4", "doc-5" };

    [TestInitialize]
    public Task SeedVectors() =>
        this.PutVectors(SeededKeys.Select(key => new SeededVector(key, new[] { 0.5f, 0.25f })).ToArray());

    [TestMethod]
    public async Task ListVectorsAsync_ShouldReadEveryVectorExactlyOnce_GivenWorkersScanningAllSegmentsInParallel()
    {
        var scans = await Task.WhenAll(Enumerable.Range(0, SegmentCount).Select(this.ScanSegment));
        scans.SelectMany(keys => keys).Should().BeEquivalentTo(SeededKeys, options => options.WithoutStrictOrdering(),
            "the segments are disjoint and together cover the index, so the union holds every key once");
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldAcceptTheMaximumSegmentCount()
    {
        var page = await this.Index.ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(15, 16) });
        page.Items.Select(vector => vector.Key).Should().BeSubsetOf(SeededKeys, "the service accepts a segment count up to 16 and a segment may hold no vector");
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldThrowInvalidRequest_GivenASegmentCountAbove16()
    {
        var thrown = await FluentActions.Awaiting(() => this.Index.ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(0, 17) }))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the segment count range");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
        }
    }

    private async Task<IReadOnlyList<string>> ScanSegment(int segmentIndex)
    {
        var keys = new List<string>();
        var options = new ListVectorsOptions { MaxResults = 1, Segment = new VectorSegment(segmentIndex, SegmentCount) };
        VectorListPage<StoredVector> page;
        do
        {
            page = await this.Index.ListVectorsAsync(options);
            keys.AddRange(page.Items.Select(vector => vector.Key));
            options = options with { NextToken = page.NextToken };
        }
        while (page.NextToken is not null);
        return keys;
    }
}
