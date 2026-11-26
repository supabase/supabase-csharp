using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.Common;

namespace Storage.Tests.Common;

/// <summary>
/// Unit tests for the defaults of the page every paged Storage list returns.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PageTests
{
    [TestMethod]
    public void Page_ShouldHaveNoItem_GivenNoneSet() =>
        new Page<string>().Items.Should().BeEmpty("a page is never null, so callers can iterate it without a check");
}
