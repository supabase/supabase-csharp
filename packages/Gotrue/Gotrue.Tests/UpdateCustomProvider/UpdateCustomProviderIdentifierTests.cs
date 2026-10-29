#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.UpdateCustomProvider;

#endregion

namespace Gotrue.Tests.UpdateCustomProvider;

/// <summary>
///     The identifier checks made before an update leaves the SDK: the <c>custom:</c> prefix the auth server requires,
///     and the characters that would change which route the request reaches.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateCustomProviderIdentifierTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    [DataRow("mycompany")]
    [DataRow("custom:x/../../users/00000000-0000-0000-0000-000000000000")]
    public async Task UpdateCustomProviderAsync_ShouldThrowArgument_GivenIdentifierItCannotRoute(string identifier)
    {
        var update = () => Admin.UpdateCustomProviderAsync(identifier, new CustomProviderChanges { Name = "Renamed" });
        await update.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName("identifier", "the identifier is a path segment checked like the delete's: 'custom:' prefix, no route-changing characters");
    }
}
