#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.GetCustomProvider;

/// <summary>
///     The identifier checks made before a lookup leaves the SDK: the <c>custom:</c> prefix the auth server requires,
///     and the characters that would change which route the request reaches.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class GetCustomProviderIdentifierTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldThrowArgumentNull_GivenNullIdentifier()
    {
        var get = () => Admin.GetCustomProviderAsync(null!);
        await get.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("identifier");
    }

    [TestMethod]
    [DataRow("mycompany")]
    [DataRow("custom:x/../../users/00000000-0000-0000-0000-000000000000")]
    public async Task GetCustomProviderAsync_ShouldThrowArgument_GivenIdentifierItCannotRoute(string identifier)
    {
        var get = () => Admin.GetCustomProviderAsync(identifier);
        await get.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName("identifier", "the identifier is a path segment checked like the delete's: 'custom:' prefix, no route-changing characters");
    }
}
