using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.NamespaceExists;

namespace Storage.Tests.NamespaceExists;

/// <summary>
/// Unit tests for how a namespace is written into a request path.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IcebergNamespacePathTests
{
    [TestMethod]
    public void For_ShouldEscapeAndJoinLevelsWithTheEncodedUnitSeparator() =>
        IcebergNamespacePath.For(IcebergNamespace.Of("a b", "c/d")).Should().Be("a%20b%1Fc%2Fd");

    [TestMethod]
    public void For_ShouldAcceptLevelsContainingDots() =>
        IcebergNamespacePath.For(IcebergNamespace.Of("a..b")).Should().Be("a..b");

    [TestMethod]
    public void For_ShouldAcceptDotLevelsInAMultiLevelNamespace() =>
        IcebergNamespacePath.For(IcebergNamespace.Of("..", "a")).Should().Be("..%1Fa", "the joined segment is no longer a relative one");
}
