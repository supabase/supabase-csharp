using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.AnalyticsBuckets.CreateBucket;

/// <summary>
/// Unit tests for the only check <c>Analytics.CreateBucketAsync</c> makes before the wire. The name format
/// itself is validated by the service.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CreateAnalyticsBucketTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task CreateBucketAsync_ShouldThrowArgumentException_GivenBlankName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Analytics.CreateBucketAsync(name!))
            .Should().ThrowAsync<ArgumentException>("a blank name cannot name a bucket"))
            .WithMessage("An analytics bucket name*");
    }
}
