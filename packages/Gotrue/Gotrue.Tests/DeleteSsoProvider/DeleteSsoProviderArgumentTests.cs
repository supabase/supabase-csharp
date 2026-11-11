#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.DeleteSsoProvider;

/// <summary>
///     The argument check made before a deletion leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteSsoProviderArgumentTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowArgumentNull_GivenNullProvider()
    {
        var delete = () => Admin.DeleteSsoProviderAsync(null!);
        await delete.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("provider");
    }
}
