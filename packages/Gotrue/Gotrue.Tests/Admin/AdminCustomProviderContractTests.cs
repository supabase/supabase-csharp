#region

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Pins the wire contract of custom OAuth/OIDC provider administration against a stubbed auth server:
///     the route, the service-role credentials, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminCustomProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/custom-providers/*")).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    [DataRow("custom:mycompany")]
    [DataRow("custom:my.company")]
    public async Task DeleteCustomProviderAsync_ShouldDeleteProviderByIdentifier(string identifier)
    {
        await new AdminClient(ServiceKey, new ClientOptions { Url = this.server.Url }).DeleteCustomProviderAsync(identifier);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath($"/admin/custom-providers/{identifier}")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task DeleteCustomProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var delete = () => new AdminClient(ServiceKey, new ClientOptions { Url = this.server.Url })
            .DeleteCustomProviderAsync("custom:mycompany", cancelled.Token);
        await delete.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }
}
