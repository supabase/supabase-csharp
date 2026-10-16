using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Requests;

/// <summary>
///     A response slower than <see cref="ClientOptions.Timeout" /> fails the request.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class TimeoutTests
{
    [TestMethod]
    public async Task Get_ShouldTimeOut_GivenResponseSlowerThanTimeout()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithBody("[]").WithDelay(TimeSpan.FromMilliseconds(200)));
        var client = new Client(server.Url!, new ClientOptions { Timeout = TimeSpan.FromMilliseconds(20) });
        var act = () => client.Table<Todo>().Get();
        (await act.Should().ThrowAsync<TaskCanceledException>()).Which.InnerException.Should().BeOfType<TimeoutException>();
    }
}
