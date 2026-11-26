using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using VerifyMSTest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Support;

/// <summary>
///     Base fixture for request approval (Contract-tier) tests. It points a real <see cref="Client" /> at a
///     hermetic WireMock server, drives the request under test, and snapshots the exact body the SDK put on
///     the wire. Capturing what WireMock received — rather than serializing a DTO in isolation — pins the whole
///     transport path (serializer settings, per-property null handling, content type), which is the contract
///     the System.Text.Json migration must preserve. Each test stubs the response its operation expects via
///     <see cref="RespondWith" /> so the call completes and its request is logged.
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
        this.Client = new Client(this.server.Url!,
            new Dictionary<string, string> { { "Authorization", "Bearer test-key" } });
    }

    [TestCleanup]
    public void DisposeApprovalServer() => this.server.Stop();

    /// <summary>Answers every route with <c>200</c> and the given JSON, so the operation under test completes.</summary>
    protected void RespondWith(string json) =>
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(json));

    /// <summary>
    ///     Clears the requests received so far, so a test can prepare its input through earlier calls (for example
    ///     loading a table) and still snapshot only the request under test.
    /// </summary>
    protected void ForgetEmittedRequests() => this.server.ResetLogEntries();

    /// <summary>
    ///     The exact bytes of the single request the SDK emitted. Tests pass this to <c>Verify</c> from their
    ///     own method so the snapshot file lands next to the test (domain-first layout), not beside this fixture.
    /// </summary>
    protected string EmittedRequestBody =>
        this.server.LogEntries.Should().ContainSingle("the SDK should emit exactly one request")
            .Which.RequestMessage!.Body!;
}
