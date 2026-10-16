using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Reading;

/// <summary>
///     <c>StripNulls()</c> sets the Accept header of the next request.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StripNullsTests
{
    [TestMethod]
    public async Task StripNulls_ShouldOnlyApplyToTheNextRequest()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().UsingAnyMethod()).RespondWith(Response.Create().WithBody("[]"));
        var table = new Client(server.Url!).Table<Todo>().StripNulls();
        await table.Get();
        await table.Get();
        server.LogEntries.Select(entry => entry.RequestMessage!.Headers!.TryGetValue("Accept", out var accept) ? accept[0] : null)
            .Should().Equal("application/vnd.pgrst.array+json;nulls=stripped", null);
    }
}
