using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Functions;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Functions.Client;

namespace Functions.Tests.AccessToken;

/// <summary>
/// How an invocation authenticates when <see cref="ClientOptions.RetrieveAccessToken"/> supplies the token:
/// the retrieved token replaces the client-level sources, the invocation's own token or Authorization header
/// still wins, and the callback is awaited on every invocation with the caller's cancellation token.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class InvokeAccessTokenTests
{
    private const string FunctionName = "hello";
    private const string RetrievedToken = "retrieved-token";

    private WireMockServer server = null!;
    private Func<CancellationToken, Task<string?>> retrieveAccessToken = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));
        this.retrieveAccessToken = Substitute.For<Func<CancellationToken, Task<string?>>>();
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns(RetrievedToken);
        this.client = new Client($"{this.server.Url}/functions/v1", options: new ClientOptions { RetrieveAccessToken = this.retrieveAccessToken });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task Invoke_ShouldSendTheRetrievedToken_GivenRetrieveAccessToken()
    {
        await this.client.Invoke(FunctionName);
        AuthorizationOf(this.SingleRequest()).Should().Be($"Bearer {RetrievedToken}");
    }

    [TestMethod]
    public async Task Invoke_ShouldPreferTheRetrievedToken_GivenSetAuth()
    {
        this.client.SetAuth("session-token");
        await this.client.Invoke(FunctionName);
        AuthorizationOf(this.SingleRequest()).Should().Be($"Bearer {RetrievedToken}", "the retrieved token replaces the client-level token");
    }

    [TestMethod]
    public async Task Invoke_ShouldPreferTheRetrievedToken_GivenAuthorizationFromGetHeaders()
    {
        this.client.GetHeaders = () => new Dictionary<string, string> { { "Authorization", "Bearer dynamic-token" } };
        await this.client.Invoke(FunctionName);
        AuthorizationOf(this.SingleRequest()).Should().Be($"Bearer {RetrievedToken}", "the retrieved token replaces the client-level headers' Authorization");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendTheTokenArgument_GivenRetrieveAccessToken()
    {
        await this.client.Invoke(FunctionName, "invocation-token");
        AuthorizationOf(this.SingleRequest()).Should().Be("Bearer invocation-token", "a token passed to the invocation is explicit and wins");
    }

    [TestMethod]
    [DataRow("Authorization")]
    [DataRow("authorization")]
    public async Task Invoke_ShouldKeepTheInvocationAuthorization_GivenRetrieveAccessToken(string headerName)
    {
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { Headers = { { headerName, "Bearer invocation-header" } } });
        AuthorizationOf(this.SingleRequest()).Should().Be("Bearer invocation-header", "an Authorization header set on the invocation is explicit and wins, whatever its casing");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendTheSetAuthToken_GivenRetrieveAccessTokenReturnsNull()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns((string?) null);
        this.client.SetAuth("session-token");
        await this.client.Invoke(FunctionName);
        AuthorizationOf(this.SingleRequest()).Should().Be("Bearer session-token", "a null token leaves the headers unchanged");
    }

    [TestMethod]
    public async Task Invoke_ShouldRetrieveTheTokenForEachInvocation()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns("first-token", "second-token");
        await this.client.Invoke(FunctionName);
        await this.client.Invoke(FunctionName);
        this.server.LogEntries.Select(entry => AuthorizationOf(entry.RequestMessage!)).Should().Equal(["Bearer first-token", "Bearer second-token"], "the token is never cached");
    }

    [TestMethod]
    public async Task Invoke_ShouldPassTheCancellationTokenToRetrieveAccessToken()
    {
        using var cancellation = new CancellationTokenSource();
        await this.client.Invoke(FunctionName, cancellationToken: cancellation.Token);
        await this.retrieveAccessToken.Received(1).Invoke(cancellation.Token);
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowTheRetrievalFailure_GivenRetrieveAccessTokenThrows()
    {
        var failure = new InvalidOperationException("token provider unavailable");
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns<Task<string?>>(_ => throw failure);
        var act = () => this.client.Invoke(FunctionName);
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure, "the caller's own failure surfaces unwrapped");
    }

    [TestMethod]
    public void InvokeStream_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns(_ => RetrievedLater(RetrievedToken));
        BlockingCaller.Completes(() => this.client.InvokeStream(FunctionName)).Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static Task<string?> RetrievedLater(string token) =>
        Task.Run<string?>(async () =>
        {
            await Task.Delay(1).ConfigureAwait(false);
            return token;
        });

    private IRequestMessage SingleRequest() => this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;

    private static string AuthorizationOf(IRequestMessage request) => request.Headers!["Authorization"].Single();
}
