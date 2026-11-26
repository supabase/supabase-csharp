using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.Common;

namespace Storage.Tests.Common;

/// <summary>
/// Unit tests for how primitive Iceberg types compare. Their wire names are pinned by the create-table approval tests.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IcebergPrimitiveTypeTests
{
    [TestMethod]
    public void Equals_ShouldBeTrue_GivenSameParameters() =>
        IcebergPrimitiveType.Decimal(10, 2).Should().Be(IcebergPrimitiveType.Decimal(10, 2));

    [TestMethod]
    public void Equals_ShouldBeFalse_GivenDifferentTypes() =>
        IcebergPrimitiveType.Int.Should().NotBe(IcebergPrimitiveType.Long);
}
