#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.UpdateSsoProvider;

#endregion

namespace Gotrue.Tests.UpdateSsoProvider;

/// <summary>
///     The changes to a provider: the current provider their <c>NameID</c> format is kept from.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SsoProviderChangesTests
{
    [TestMethod]
    public void KeepingNameIdFormatOf_ShouldThrowArgumentNull_GivenNullProvider()
    {
        var changes = () => SsoProviderChanges.KeepingNameIdFormatOf(null!);
        changes.Should().ThrowExactly<ArgumentNullException>().WithParameterName("current");
    }
}
