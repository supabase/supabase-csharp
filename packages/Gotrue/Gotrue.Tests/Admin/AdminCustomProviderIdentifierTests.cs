#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     The identifier checks made before a custom provider request leaves the SDK: the <c>custom:</c> prefix the
///     auth server requires, and the characters that would change which route the request reaches.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AdminCustomProviderIdentifierTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task DeleteCustomProviderAsync_ShouldThrowArgumentNull_GivenNullIdentifier()
    {
        var delete = () => Admin.DeleteCustomProviderAsync(null!);
        await delete.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("identifier");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("mycompany")]
    [DataRow("Custom:mycompany")]
    [DataRow(" custom:mycompany")]
    public async Task DeleteCustomProviderAsync_ShouldThrowArgument_GivenIdentifierWithoutCustomPrefix(string identifier)
    {
        var delete = () => Admin.DeleteCustomProviderAsync(identifier);
        await delete.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage("*start with 'custom:'*", "the auth server rejects any identifier without the case-sensitive 'custom:' prefix with 400 validation_failed")
            .WithParameterName("identifier");
    }

    [TestMethod]
    [DataRow("custom:my/company")]
    [DataRow("custom:x/../../users/00000000-0000-0000-0000-000000000000")]
    [DataRow("custom:my\\company")]
    [DataRow("custom:my?company")]
    [DataRow("custom:my#company")]
    [DataRow("custom:my%2Fcompany")]
    public async Task DeleteCustomProviderAsync_ShouldThrowArgument_GivenIdentifierThatChangesTheRoute(string identifier)
    {
        var delete = () => Admin.DeleteCustomProviderAsync(identifier);
        await delete.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage("*cannot contain*", "the identifier is a path segment: these characters would send the delete to another route, such as /admin/users/{id}")
            .WithParameterName("identifier");
    }
}
