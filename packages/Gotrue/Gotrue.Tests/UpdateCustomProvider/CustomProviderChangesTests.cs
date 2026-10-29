#region

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.UpdateCustomProvider;

#endregion

namespace Gotrue.Tests.UpdateCustomProvider;

/// <summary>
///     Changes to a custom provider: keeping a new client secret out of logs.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CustomProviderChangesTests
{
    [TestMethod]
    public void ToString_ShouldLeaveOutClientSecret() =>
        new CustomProviderChanges { Name = "Renamed", ClientId = "client-id", ClientSecret = "client-secret" }.ToString().Should().Be(
            "CustomProviderChanges { Name = Renamed, ClientId = client-id, Issuer = , AuthorizationUrl = , TokenUrl = , UserInfoUrl = , Options =  }",
            "a record prints every property by default, and the secret must never reach a log");
}
