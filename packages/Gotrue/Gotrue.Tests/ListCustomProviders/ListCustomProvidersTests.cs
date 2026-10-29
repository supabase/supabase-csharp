#region

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.ListCustomProviders;

/// <summary>
///     End-to-end listing of custom OAuth/OIDC providers against the live stack, with and without a type filter.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ListCustomProvidersTests : CustomProviderAdminFixture
{
    [TestMethod]
    [DataRow(null, true)]
    [DataRow(CustomProviderType.OAuth2, true)]
    [DataRow(CustomProviderType.Oidc, false)]
    public async Task ListCustomProvidersAsync_ShouldListSeededOAuth2ProviderOnlyWhenTypeMatches(CustomProviderType? type, bool listed)
    {
        await this.SeedProvider();
        (await this.Admin.ListCustomProvidersAsync(type)).Any(provider => provider.Identifier == this.Identifier).Should().Be(listed,
            "the auth server filters the list by provider_type when type is given and returns every provider otherwise");
    }

    [TestMethod]
    public async Task ListCustomProvidersAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var list = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).ListCustomProvidersAsync();
        await list.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
