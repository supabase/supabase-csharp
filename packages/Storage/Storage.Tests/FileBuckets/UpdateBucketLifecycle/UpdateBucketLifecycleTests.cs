using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.FileBuckets.UpdateBucketLifecycle;

/// <summary>
/// Unit tests for the checks made before the wire when updating a bucket lifecycle policy.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateBucketLifecycleTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldThrowArgumentNullException_GivenNoPolicy() =>
        (await FluentActions.Awaiting(() => this.sut.UpdateBucketLifecycleAsync("avatars", null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no policy to store"))
            .Which.ParamName.Should().Be("lifecycle");
}
