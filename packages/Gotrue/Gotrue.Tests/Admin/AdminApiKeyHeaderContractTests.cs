#region

using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Pins the apikey header contract for a standalone <see cref="AdminClient" /> (constructed directly, not
///     wired through the meta client). New-format keys (sb_publishable_/sb_secret_) are rejected on
///     Authorization-only requests, so every authed admin request must also carry an <c>apikey</c> header
///     (supabase-community/gotrue-csharp#119).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminApiKeyHeaderContractTests
{
    private const string ServiceKey = "sb_secret_service_role_key";
    private const string UserId = "user-123";

    // JWT-shaped test token; no valid signature needed.
    private const string UserToken =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiJ1c2VyLTEyMyIsInJvbGUiOiJhdXRoZW50aWNhdGVkIiwiZXhwIjoyMDAwMDAwMDAwfQ." +
        "not-a-real-signature";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().WithPath($"/admin/users/{UserId}").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.server.Given(Request.Create().WithPath("/user").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task DeleteUser_ShouldSendApiKeyHeader_GivenNoApiKeyConfigured()
    {
        var admin = new AdminClient(ServiceKey, new ClientOptions { Url = this.server.Url });
        await admin.DeleteUser(UserId);
        this.server.VerifySingleReceivedRequest()
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithHeader("apikey", ServiceKey);
    }

    [TestMethod]
    public async Task DeleteUser_ShouldPreserveConfiguredApiKey_GivenApiKeyInHeaders()
    {
        var admin = new AdminClient(ServiceKey, new ClientOptions
        {
            Url = this.server.Url,
            Headers = new Dictionary<string, string> { ["apikey"] = "project-anon-key" },
        });
        await admin.DeleteUser(UserId);
        this.server.VerifySingleReceivedRequest()
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithHeader("apikey", "project-anon-key");
    }

    [TestMethod]
    public async Task DeleteUser_ShouldNotDuplicateApiKey_GivenMetaStyleCasing()
    {
        // The meta client stores the project key as "apiKey" (capital K); the guard must match it
        // case-insensitively, otherwise a second lowercase "apikey" is appended and the gateway sees two.
        var admin = new AdminClient(ServiceKey, new ClientOptions
        {
            Url = this.server.Url,
            Headers = new Dictionary<string, string> { ["apiKey"] = "project-anon-key" },
        });
        await admin.DeleteUser(UserId);
        this.server.VerifySingleReceivedRequest().WithHeader("apiKey", "project-anon-key");
    }

    [TestMethod]
    public async Task GetUser_ShouldNotSendUserTokenAsApiKey_GivenNoApiKeyConfigured()
    {
        // Copying a user token into apikey caused a 401 (#424).
        var admin = new AdminClient(ServiceKey, new ClientOptions { Url = this.server.Url });
        await admin.GetUser(UserToken);
        this.server.VerifySingleReceivedRequest()
            .WithHeader("Authorization", $"Bearer {UserToken}")
            .WithoutHeader("apikey");
    }
}
