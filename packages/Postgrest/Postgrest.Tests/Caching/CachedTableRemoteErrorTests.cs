using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Caching;

/// <summary>
///     A failed background load of a cached <c>Get()</c> is reported to the debug handlers instead of crashing the app.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CachedTableRemoteErrorTests
{
    [TestMethod]
    public async Task Get_ShouldReportTheRemoteError_GivenServerError()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().UsingGet()).RespondWith(Response.Create().WithStatusCode(500).WithBody("{}"));
        var remoteError = new TaskCompletionSource<PostgrestException>(TaskCreationOptions.RunContinuationsAsynchronously);
        IPostgrestDebugger.DebugEventHandler handler = (_, _, exception) =>
        {
            if (exception != null) remoteError.TrySetResult(exception);
        };
        Debugger.Instance.AddDebugHandler(handler);
        try
        {
            await new Client(server.Url!).Table<Todo>(Substitute.For<IPostgrestCacheProvider>()).Get();
            (await remoteError.Task.WaitAsync(TimeSpan.FromSeconds(5))).StatusCode.Should().Be(500);
        }
        finally
        {
            Debugger.Instance.RemoveDebugHandler(handler);
        }
    }
}
