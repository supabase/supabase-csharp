using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.AccessToken;

/// <summary>
///     How table queries and RPCs authenticate when <see cref="ClientOptions.RetrieveAccessToken" /> supplies the
///     token: the retrieved token replaces any configured Authorization, a null token leaves the headers unchanged,
///     and the callback is awaited on every request with the caller's cancellation token.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RequestAccessTokenTests
{
    private const string RetrievedBearer = "Bearer retrieved-token";

    private WireMockServer server = null!;
    private Func<CancellationToken, Task<string?>> retrieveAccessToken = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
        this.retrieveAccessToken = Substitute.For<Func<CancellationToken, Task<string?>>>();
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns("retrieved-token");
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task Get_ShouldSendTheRetrievedToken()
    {
        await this.ClientWith().Table<Todo>().Get();
        this.SingleAuthorization().Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    [DataRow("Authorization")]
    [DataRow("authorization")]
    public async Task Get_ShouldReplaceTheConfiguredAuthorization_GivenRetrieveAccessToken(string headerName)
    {
        await this.ClientWith(new Dictionary<string, string> { { headerName, "Bearer configured-key" } }).Table<Todo>().Get();
        this.SingleAuthorization().Should().Equal([RetrievedBearer], "header names are case-insensitive, so the retrieved token replaces rather than joins it");
    }

    [TestMethod]
    public async Task Get_ShouldReplaceTheDynamicAuthorization_GivenRetrieveAccessToken()
    {
        var client = this.ClientWith();
        client.GetHeaders = () => new Dictionary<string, string> { { "Authorization", "Bearer session-token" } };
        await client.Table<Todo>().Get();
        this.SingleAuthorization().Should().Equal([RetrievedBearer], "the retrieved token replaces the client's dynamic Authorization");
    }

    [TestMethod]
    public async Task Get_ShouldKeepTheConfiguredAuthorization_GivenRetrieveAccessTokenReturnsNull()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns((string?) null);
        await this.ClientWith(new Dictionary<string, string> { { "Authorization", "Bearer configured-key" } }).Table<Todo>().Get();
        this.SingleAuthorization().Should().Equal(["Bearer configured-key"], "a null token leaves the headers unchanged");
    }

    [TestMethod]
    public async Task Get_ShouldRetrieveTheTokenForEachRequest()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns("first-token", "second-token");
        var table = this.ClientWith().Table<Todo>();
        await table.Get();
        await table.Get();
        this.server.LogEntries.Select(entry => Authorization(entry.RequestMessage!).Single()).Should().Equal(["Bearer first-token", "Bearer second-token"], "the token is never cached");
    }

    [TestMethod]
    public async Task Get_ShouldPassTheCancellationTokenToRetrieveAccessToken()
    {
        using var cancellation = new CancellationTokenSource();
        await this.ClientWith().Table<Todo>().Get(cancellation.Token);
        await this.retrieveAccessToken.Received(1).Invoke(cancellation.Token);
    }

    [TestMethod]
    public async Task Get_ShouldThrowTheRetrievalFailure_GivenRetrieveAccessTokenThrows()
    {
        var failure = new InvalidOperationException("token provider unavailable");
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns<Task<string?>>(_ => throw failure);
        var act = () => this.ClientWith().Table<Todo>().Get();
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure, "the caller's own failure surfaces unwrapped");
    }

    [TestMethod]
    public async Task Rpc_ShouldSendTheRetrievedToken()
    {
        await this.ClientWith().Rpc("list_todos");
        this.SingleAuthorization().Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public void Rpc_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns(_ => RetrievedLater("retrieved-token"));
        BlockingCaller.Completes(() => this.ClientWith().Rpc("list_todos")).Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static Task<string?> RetrievedLater(string token) =>
        Task.Run<string?>(async () =>
        {
            await Task.Delay(1).ConfigureAwait(false);
            return token;
        });

    private Client ClientWith(Dictionary<string, string>? headers = null) =>
        new(this.server.Url!, new ClientOptions { RetrieveAccessToken = this.retrieveAccessToken, Headers = headers ?? new Dictionary<string, string>() });

    private IEnumerable<string> SingleAuthorization() =>
        Authorization(this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!);

    private static IEnumerable<string> Authorization(IRequestMessage request) =>
        request.Headers!.Where(header => string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)).SelectMany(header => header.Value);
}
