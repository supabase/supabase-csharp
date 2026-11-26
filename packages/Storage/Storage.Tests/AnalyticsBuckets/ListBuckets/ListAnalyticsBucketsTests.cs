using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.ListBuckets;

namespace Storage.Tests.AnalyticsBuckets.ListBuckets;

/// <summary>
/// Unit tests for the only check <c>Analytics.ListBucketsAsync</c> makes before the wire. Pagination ranges are
/// validated by the service.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ListAnalyticsBucketsTests
{
    private const AnalyticsBucketSortColumn UndefinedColumn = (AnalyticsBucketSortColumn) 42;

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowArgumentOutOfRange_GivenUndefinedSortColumn()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var options = new ListAnalyticsBucketsOptions { Sort = new AnalyticsBucketSort(UndefinedColumn) };
        (await FluentActions.Awaiting(() => sut.Analytics.ListBucketsAsync(options))
            .Should().ThrowAsync<ArgumentOutOfRangeException>("the route only sorts by name, created_at or updated_at"))
            .WithMessage("The sort column is not one the service can sort analytics buckets by.*");
    }
}
