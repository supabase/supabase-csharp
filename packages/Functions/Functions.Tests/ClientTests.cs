using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions;
using static Supabase.Functions.Client;

namespace Functions.Tests;

/// <summary>
/// End-to-end tests that invoke the <c>hello</c> edge function against a running local Supabase
/// stack (started with <c>supabase start</c>), exercising the full request/response round trip for
/// the string, typed, and raw invocation shapes.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ClientTests
{
    private const string Function = "hello";

    private Client client = null!;
    private string token = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.token = LocalStackToken.Issue();
        this.client = new Client("http://localhost:54321/functions/v1");
    }

    [TestMethod]
    public async Task Invoke_ShouldReturnGreetingContainingTheName()
    {
        var result = await this.client.Invoke(Function, this.token, new InvokeFunctionOptions
        {
            Body = new Dictionary<string, object> { { "name", "supabase" } },
            HttpMethod = HttpMethod.Post
        });
        result.Should().Contain("supabase");
    }

    [TestMethod]
    public async Task Invoke_ShouldReturnDeserializedGreeting_GivenTypedInvoke()
    {
        var result = await this.client.Invoke<Dictionary<string, string>>(Function, this.token, new InvokeFunctionOptions
        {
            Body = new Dictionary<string, object> { { "name", "functions" } },
            HttpMethod = HttpMethod.Post
        });
        result.Should().ContainKey("message").WhoseValue.Should().Contain("functions");
    }

    [TestMethod]
    public async Task RawInvoke_ShouldReturnReadableBytes()
    {
        var content = await this.client.RawInvoke(Function, this.token, new InvokeFunctionOptions
        {
            Body = new Dictionary<string, object> { { "name", "functions" } },
            HttpMethod = HttpMethod.Post
        });
        (await content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task Invoke_ShouldGreetWithFunctionName_GivenGetWithoutBody()
    {
        var result = await this.client.Invoke(Function, this.token, new InvokeFunctionOptions
        {
            Body = [],
            HttpMethod = HttpMethod.Get
        });
        result.Should().Contain(Function);
    }
}
