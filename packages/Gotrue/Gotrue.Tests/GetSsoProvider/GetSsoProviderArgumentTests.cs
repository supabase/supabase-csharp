#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.GetSsoProvider;

/// <summary>
///     The argument check made before a lookup leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class GetSsoProviderArgumentTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowArgumentNull_GivenNullProvider()
    {
        var get = () => Admin.GetSsoProviderAsync(null!);
        await get.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("provider");
    }
}
