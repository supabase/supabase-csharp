using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.CreateNamespace;

namespace Storage.Tests.CreateNamespace;

/// <summary>
/// Unit tests for the checks made before the wire when creating a namespace, and for the defaults of its options.
/// The name itself is validated by the service.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CreateNamespaceTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").CreateNamespaceAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no namespace to create"))
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public void CreateNamespaceOptions_ShouldHaveNoProperties_GivenNoneSet() =>
        new CreateNamespaceOptions().Properties.Should().BeEmpty();
}
