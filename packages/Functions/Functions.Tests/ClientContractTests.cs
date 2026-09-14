using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Functions;
using Supabase.Functions.Exceptions;
using WireMock;
using WireMock.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Functions.Client;

namespace Functions.Tests;

/// <summary>
/// Contract tests for the HTTP request the client builds (path, method, auth, region and dynamic
/// headers, JSON body) and for how it interprets the response (success payload shapes, and the
/// failures — error status codes and relay errors — that surface as a <see cref="FunctionsException"/>
/// carrying its status, content, and detected reason).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ClientContractTests
{
    private const string FunctionName = "hello";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/functions/v1");
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task Invoke_ShouldReturnResponseBody()
    {
        this.RespondWith(200, "{\"message\":\"Hello supabase!\"}");
        var result = await this.client.Invoke(FunctionName);
        result.Should().Be("{\"message\":\"Hello supabase!\"}");
    }

    [TestMethod]
    public async Task Invoke_ShouldDeserializeResponse_GivenTypedInvoke()
    {
        this.RespondWith(200, "{\"message\":\"Hello supabase!\"}");
        var result = await this.client.Invoke<Dictionary<string, string>>(FunctionName);
        result.Should().Contain("message", "Hello supabase!");
    }

    [TestMethod]
    public async Task RawInvoke_ShouldReturnReadableContent()
    {
        this.RespondWith(200, "raw-payload");
        var content = await this.client.RawInvoke(FunctionName);
        (await content.ReadAsStringAsync()).Should().Be("raw-payload");
    }

    [TestMethod]
    public async Task Invoke_ShouldPostToTheFunctionPath()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName);
        var request = this.SingleRequest();
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be($"/functions/v1/{FunctionName}");
        }
    }

    [TestMethod]
    public async Task Invoke_ShouldUseTheGivenHttpMethod()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { HttpMethod = HttpMethod.Get });
        this.SingleRequest().Method.Should().Be("GET");
    }

    [TestMethod]
    public async Task Invoke_ShouldSerializeBodyAsJson()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions
        {
            Body = new Dictionary<string, object> { { "name", "supabase" } }
        });
        this.SingleRequest().Body.Should().Be("{\"name\":\"supabase\"}");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendBearerToken_GivenToken()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, "the-token");
        this.HeaderOf("Authorization").Should().Be("Bearer the-token");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendClientInfoHeader()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName);
        this.HeaderOf("X-Client-Info").Should().StartWith("supabase.functions-csharp/");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendRegionHeader_GivenOptionRegion()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { FunctionRegion = FunctionRegion.UsEast1 });
        this.HeaderOf("x-region").Should().Be("us-east-1");
    }

    [TestMethod]
    public async Task Invoke_ShouldNotSendRegionHeader_GivenAnyRegion()
    {
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { FunctionRegion = FunctionRegion.Any });
        this.SingleRequest().Headers.Should().NotContainKey("x-region");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendConstructorRegion_GivenNoOptionRegion()
    {
        this.client = new Client($"{this.server.Url}/functions/v1", FunctionRegion.EuWest2);
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName);
        this.HeaderOf("x-region").Should().Be("eu-west-2");
    }

    [TestMethod]
    public async Task Invoke_ShouldPreferOptionRegionOverConstructorRegion()
    {
        this.client = new Client($"{this.server.Url}/functions/v1", FunctionRegion.EuWest2);
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { FunctionRegion = FunctionRegion.UsEast1 });
        this.HeaderOf("x-region").Should().Be("us-east-1");
    }

    [TestMethod]
    public async Task Invoke_ShouldMergeDynamicHeaders_GivenGetHeaders()
    {
        this.client.GetHeaders = () => new Dictionary<string, string> { { "x-dynamic", "from-callback" } };
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName);
        this.HeaderOf("x-dynamic").Should().Be("from-callback");
    }

    [TestMethod]
    public async Task Invoke_ShouldPreferOptionHeadersOverDynamicHeaders()
    {
        this.client.GetHeaders = () => new Dictionary<string, string> { { "x-shared", "from-callback" } };
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName, options: new InvokeFunctionOptions
        {
            Headers = new Dictionary<string, string> { { "x-shared", "from-options" } }
        });
        this.HeaderOf("x-shared").Should().Be("from-options");
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowFunctionsException_GivenServerError()
    {
        this.RespondWith(500, "internal boom");
        var act = () => this.client.Invoke(FunctionName);
        var exception = (await act.Should().ThrowAsync<FunctionsException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(500);
            exception.Content.Should().Be("internal boom");
            exception.Response.Should().NotBeNull();
            exception.Reason.Should().Be(FailureHint.Reason.Internal);
        }
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowNotAuthorized_GivenUnauthorized()
    {
        this.RespondWith(401, "no");
        var act = () => this.client.Invoke(FunctionName);
        (await act.Should().ThrowAsync<FunctionsException>()).Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowFunctionsException_GivenRelayErrorOnSuccessStatus()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("x-relay-error", "true").WithBody("relayed"));
        var act = () => this.client.Invoke(FunctionName);
        (await act.Should().ThrowAsync<FunctionsException>()).Which.Content.Should().Be("relayed");
    }

    [TestMethod]
    public async Task Invoke_ShouldSendThroughTheInjectedHttpClient_GivenClientOptions()
    {
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        this.client = new Client($"{this.server.Url}/functions/v1", options: new ClientOptions { HttpClient = injectedClient });
        this.RespondWith(200, "ok");
        await this.client.Invoke(FunctionName);
        this.HeaderOf("X-Injected").Should().Be("true", "the client must send through the caller-supplied HttpClient, not build its own");
    }

    [TestMethod]
    public async Task Invoke_ShouldRetryUntilSuccess_GivenRetryableStatus()
    {
        this.client = new Client($"{this.server.Url}/functions/v1", options: new ClientOptions
        {
            Retry = new RetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) }
        });
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .InScenario("retry").WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(503));
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .InScenario("retry").WhenStateIs("retried")
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("recovered"));

        var result = await this.client.Invoke(FunctionName);

        result.Should().Be("recovered");
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowTimeoutException_GivenResponseSlowerThanHttpTimeout()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromMilliseconds(200)));
        var act = () => this.client.Invoke(FunctionName, options: new InvokeFunctionOptions { HttpTimeout = TimeSpan.FromMilliseconds(20) });
        (await act.Should().ThrowAsync<TaskCanceledException>()).Which.InnerException.Should().BeOfType<TimeoutException>();
    }

    [TestMethod]
    public async Task Invoke_ShouldThrowOperationCanceledException_GivenCallerCancellation()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromMilliseconds(200)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var act = () => this.client.Invoke(FunctionName, cancellationToken: cts.Token);
        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.InnerException.Should().NotBeOfType<TimeoutException>(
            "caller-triggered cancellation must not be mis-reported as an HttpTimeout");
    }

    [TestMethod]
    public async Task InvokeStream_ShouldReturnBeforeBodyCompletes()
    {
        var holdBodyOpen = new TaskCompletionSource();
        this.RespondWithEvents(async queue =>
        {
            queue.Write("data: first\n\n");
            await holdBodyOpen.Task;
        });

        using var response = await this.client.InvokeStream(FunctionName).WaitAsync(Deadline);
        holdBodyOpen.SetResult();

        (await response.Content.ReadAsStringAsync()).Should().Be("data: first\n\n");
    }

    [TestMethod]
    public async Task InvokeStream_ShouldBufferTheErrorBody_GivenServerError()
    {
        this.RespondWith(500, "internal boom");
        var act = () => this.client.InvokeStream(FunctionName);
        var exception = (await act.Should().ThrowAsync<FunctionsException>()).Which;
        (await exception.Response!.Content.ReadAsStringAsync()).Should().Be("internal boom");
    }

    [TestMethod]
    public async Task InvokeStream_ShouldThrowTimeoutException_GivenAStalledErrorBody()
    {
        var holdBodyOpen = new TaskCompletionSource();
        // WireMock forces 200 on an SSE body, so a relay error is what makes this one fail.
        this.RespondWithEvents(async queue =>
        {
            queue.Write("partial");
            await holdBodyOpen.Task;
        }, Response.Create().WithHeader("x-relay-error", "true"));

        var act = () => this.client.InvokeStream(FunctionName, options: new InvokeFunctionOptions { HttpTimeout = TimeSpan.FromMilliseconds(100) });
        var exception = (await act.Should().ThrowAsync<TaskCanceledException>().WaitAsync(Deadline)).Which;
        holdBodyOpen.SetResult();

        exception.InnerException.Should().BeOfType<TimeoutException>();
    }

    private void RespondWith(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithHeader("Content-Type", "application/json").WithBody(body));

    private void RespondWithEvents(Func<IBlockingQueue<string?>, Task> body, IResponseBuilder? response = null) =>
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingAnyMethod())
            .RespondWith((response ?? Response.Create()).WithHeader("Content-Type", "text/event-stream")
                .WithSseBody(async (_, queue) =>
                {
                    await body(queue);
                    queue.Close();
                }));

    private IRequestMessage SingleRequest() => this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;

    private string HeaderOf(string name) => this.SingleRequest().Headers![name][0];
}
