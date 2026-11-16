using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;

namespace Postgrest.Tests.AccessToken;

/// <summary>
///     Queries the local Supabase stack with the token supplied by <see cref="ClientOptions.RetrieveAccessToken" />:
///     PostgREST verifies that token, so a valid one is let through and a malformed one is rejected.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RequestWithRetrievedAccessTokenTests
{
    [TestMethod]
    public async Task Get_ShouldReturnRows_GivenRetrievedValidToken()
    {
        var response = await ClientRetrieving(LocalStack.AnonKey).Table<User>().Get();
        response.Models.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task Get_ShouldBeRejected_GivenRetrievedMalformedToken()
    {
        var act = () => ClientRetrieving("not-a-jwt").Table<User>().Get();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.StatusCode.Should().Be((int) HttpStatusCode.Unauthorized, "PostgREST verifies the retrieved token");
    }

    private static Client ClientRetrieving(string accessToken) =>
        LocalStack.Client(new ClientOptions { RetrieveAccessToken = _ => Task.FromResult<string?>(accessToken) });
}
