#region

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;

#endregion

namespace Gotrue.Tests.CreateOAuthClient;

/// <summary>
///     Printing an OAuth client together with its secret.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class OAuthClientCredentialsTests
{
    [TestMethod]
    public void ToString_ShouldPrintClientWithoutClientSecret() =>
        new OAuthClientCredentials { Client = new OAuthClient { Name = "My App" }, ClientSecret = "s3cr3t-value" }.ToString().Should()
            .StartWith("OAuthClientCredentials { Client = OAuthClient { ClientId = ").And.Contain("Name = My App").And.EndWith(" } }")
            .And.NotContain("s3cr3t-value", "a record prints every property by default, and the secret must never reach a log");
}
