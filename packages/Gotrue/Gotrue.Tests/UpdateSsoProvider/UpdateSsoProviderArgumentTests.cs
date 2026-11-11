#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.UpdateSsoProvider;

#endregion

namespace Gotrue.Tests.UpdateSsoProvider;

/// <summary>
///     The argument checks made before a change leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateSsoProviderArgumentTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowArgumentNull_GivenNullProvider()
    {
        var update = () => Admin.UpdateSsoProviderAsync(null!, SsoProviderChanges.WithoutNameIdFormat());
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("provider");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowArgumentNull_GivenNullChanges()
    {
        var update = () => Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(Guid.NewGuid()), null!);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("changes");
    }
}
