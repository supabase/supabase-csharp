using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;
using VerifyMSTest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Support;

/// <summary>
///     Base fixture for request approval (Contract-tier) tests. It points a real <see cref="Client" /> at a
///     hermetic WireMock server that answers every route with an empty <c>200 []</c>, drives the request under
///     test, and snapshots the exact body the SDK put on the wire. Capturing what WireMock received — rather
///     than serializing a model in isolation — pins the whole transport path, including the per-operation
///     serializer options (which drop columns per insert/update/upsert) and the custom converters, which is
///     the contract the System.Text.Json migration must preserve.
/// </summary>
public abstract class RequestApprovalFixture : VerifyBase
{
    private WireMockServer server = null!;

    /// <summary>The client under test, pointed at the hermetic server.</summary>
    protected Client Client { get; private set; } = null!;

    [TestInitialize]
    public void InitializeApprovalServer()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody("[]"));
        this.Client = new Client(this.server.Url!, new ClientOptions());
    }

    [TestCleanup]
    public void DisposeApprovalServer() => this.server.Stop();

    /// <summary>
    ///     The exact bytes of the single request the SDK emitted. Tests pass this to <c>Verify</c> from their
    ///     own method so the snapshot file lands next to the test (domain-first layout), not beside this fixture.
    /// </summary>
    protected string EmittedRequestBody =>
        this.server.LogEntries.Should().ContainSingle("the SDK should emit exactly one request")
            .Which.RequestMessage!.Body!;

    /// <summary>The <c>Prefer</c> header of each request the SDK emitted, in order.</summary>
    protected IEnumerable<string> EmittedPreferHeaders =>
        this.server.LogEntries.Select(entry => entry.RequestMessage!.Headers!["Prefer"][0]);
}
