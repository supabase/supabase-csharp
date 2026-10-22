#region

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using VerifyMSTest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     Base fixture for request approval (Contract-tier) tests. It points a real <see cref="Api" /> at a
///     hermetic <see cref="MockGotrueServer" /> that answers every route with an empty <c>200 {}</c>, drives
///     the request under test, and snapshots the exact body the SDK put on the wire. Capturing what WireMock
///     received — rather than serializing a DTO in isolation — pins the whole transport path (serializer
///     settings, content type, casing), which is the contract the System.Text.Json migration must preserve.
/// </summary>
public abstract class RequestApprovalFixture : VerifyBase
{
    private MockGotrueServer server = null!;

    /// <summary>The client under test, pointed at the hermetic server.</summary>
    protected Api Api { get; private set; } = null!;

    [TestInitialize]
    public void InitializeApprovalServer()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.Api = new Api(this.server.Url);
    }

    [TestCleanup]
    public void DisposeApprovalServer() => this.server.Dispose();

    /// <summary>
    ///     The exact bytes of the single request the SDK emitted. Tests pass this to <c>Verify</c> from their
    ///     own method so the snapshot file lands next to the test (domain-first layout), not beside this fixture.
    /// </summary>
    protected string EmittedRequestBody => this.server.VerifySingleReceivedRequest().RawBody;

    /// <summary>
    ///     Check for the request query parameter.
    /// </summary>
    /// <param name="name">Query parameter name</param>
    /// <param name="expected">Value expected</param>
    protected void ValidateQueryParameters(string name, string expected) => this.server.VerifySingleReceivedRequest()
        .WithQueryParam(name, expected);
}
