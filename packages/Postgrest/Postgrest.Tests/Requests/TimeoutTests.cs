using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Core.Http;
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

    [TestMethod]
    public async Task Get_ShouldTimeOutOnTheRetryClock_GivenACustomTimeProvider()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithBody("[]").WithDelay(TimeSpan.FromSeconds(3)));
        var clock = new FakeTimeProvider();
        var client = new Client(server.Url!, new ClientOptions { Timeout = TimeSpan.FromSeconds(1), Retry = new RetryOptions { TimeProvider = clock } });

        var get = client.Table<Todo>().Get();
        await Task.Delay(100);
        get.IsCompleted.Should().BeFalse("the timeout waits on the supplied clock, not the wall clock");
        clock.Advance(TimeSpan.FromSeconds(1));

        var act = () => get;
        (await act.Should().ThrowAsync<TaskCanceledException>().WaitAsync(TimeSpan.FromSeconds(5))).Which.InnerException.Should().BeOfType<TimeoutException>();
    }
}
