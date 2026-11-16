using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions;
using Supabase.Functions.Exceptions;
using static Supabase.Functions.Client;

namespace Functions.Tests.AccessToken;

/// <summary>
/// Invokes the <c>hello</c> edge function on the local Supabase stack with the token supplied by
/// <see cref="ClientOptions.RetrieveAccessToken"/>: the gateway verifies that token, so a valid one is let
/// through and a malformed one is rejected.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class InvokeWithRetrievedAccessTokenTests
{
    private const string Function = "hello";
    private const string FunctionsUrl = "http://localhost:54321/functions/v1";

    [TestMethod]
    public async Task Invoke_ShouldReturnGreeting_GivenRetrievedValidToken()
    {
        var client = new Client(FunctionsUrl, options: new ClientOptions { RetrieveAccessToken = _ => Task.FromResult<string?>(LocalStackToken.Issue()) });
        var result = await client.Invoke(Function, options: new InvokeFunctionOptions { Body = new Dictionary<string, object> { { "name", "retrieved" } }, HttpMethod = HttpMethod.Post });
        result.Should().Contain("retrieved");
    }

    [TestMethod]
    public async Task Invoke_ShouldBeRejected_GivenRetrievedMalformedToken()
    {
        var client = new Client(FunctionsUrl, options: new ClientOptions { RetrieveAccessToken = _ => Task.FromResult<string?>("not-a-jwt") });
        var act = () => client.Invoke(Function, options: new InvokeFunctionOptions { Body = new Dictionary<string, object> { { "name", "retrieved" } }, HttpMethod = HttpMethod.Post });
        (await act.Should().ThrowAsync<FunctionsException>()).Which.StatusCode.Should().Be((int) HttpStatusCode.Unauthorized, "the gateway verifies the retrieved token");
    }
}
