using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.DeleteBucketLifecycle;

namespace Storage.Tests.DeleteBucketLifecycle;

/// <summary>
/// Unit tests for the path of a bucket's lifecycle policy: the ids it refuses and how it escapes the rest.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class BucketLifecyclePathTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(".")]
    [DataRow("..")]
    public void For_ShouldThrowArgumentException_GivenUnusableId(string? id) =>
        FluentActions.Invoking(() => BucketLifecyclePath.For("http://h/storage/v1", id!))
            .Should().Throw<ArgumentException>("a null, empty or dot-segment id would reach another route")
            .WithMessage("A bucket id*");

    [TestMethod]
    public void For_ShouldEscapeTheIdAsOneSegment() =>
        BucketLifecyclePath.For("http://h/storage/v1", "a/b c").Should().Be("http://h/storage/v1/bucket/a%2Fb%20c/lifecycle");
}
