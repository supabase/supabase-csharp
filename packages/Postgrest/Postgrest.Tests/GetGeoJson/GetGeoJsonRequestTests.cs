using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.GetGeoJson;

/// <summary>
///     <c>GetGeoJsonAsync</c> reads with the <c>application/geo+json</c> Accept media type, returns one feature per
///     row with its geometry and its other columns as the model, attaches those models to the table, and raises
///     PostgREST's refusals as <see cref="PostgrestException" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetGeoJsonRequestTests
{
    private const string Geometry = """{"type":"Point","coordinates":[-71.10044,42.373695]}""";

    private const string CollectionBody =
        """{"type" : "FeatureCollection", "features" : [{"type": "Feature", "geometry": """ + Geometry +
        """, "properties": {"id": 1, "name": "walk the dog"}}, {"type": "Feature", "geometry": null, "properties": {"id": 2, "name": "water the plants"}}]}""";

    private WireMockServer server = null!;

    [TestInitialize]
    public void SetUp() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldSendGetWithTheGeoJsonAcceptHeader()
    {
        this.RespondWith(200, CollectionBody);
        await this.Client().Table<Todo>().GetGeoJsonAsync();
        var request = this.server.LogEntries.Single().RequestMessage!;
        (request.Method, request.Headers!["Accept"].Single())
            .Should().Be(("GET", "application/geo+json"), "PostgREST answers with a FeatureCollection when the request accepts application/geo+json");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldNotAskForNullsToBeStripped_GivenStripNulls()
    {
        this.RespondWith(200, CollectionBody);
        await this.Client().Table<Todo>().StripNulls().GetGeoJsonAsync();
        this.server.LogEntries.Single().RequestMessage!.Headers!["Accept"].Single()
            .Should().Be("application/geo+json", "nulls=stripped only applies to JSON row representations");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldReturnTheGeometryOfEachRow()
    {
        this.RespondWith(200, CollectionBody);
        (await this.Client().Table<Todo>().GetGeoJsonAsync()).Features.Select(feature => feature.Geometry?.GetRawText())
            .Should().Equal(new[] { Geometry, null }, "a row whose geometry is null has no geometry");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldReadThePropertiesOfEachRowIntoTheModel()
    {
        this.RespondWith(200, CollectionBody);
        (await this.Client().Table<Todo>().GetGeoJsonAsync()).Features.Select(feature => feature.Properties.Name)
            .Should().Equal("walk the dog", "water the plants");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldAttachThePropertiesToTheTable()
    {
        this.RespondWith(200, CollectionBody);
        var table = this.Client(new ClientOptions { Headers = new Dictionary<string, string> { ["apikey"] = "anon-key" } }).Table<Todo>();
        table.GetHeaders = () => new Dictionary<string, string> { ["Authorization"] = "Bearer user-token" };
        var collection = await table.GetGeoJsonAsync();
        await collection.Features[0].Properties.Delete<Todo>();
        var request = this.server.LogEntries.Last().RequestMessage!;
        (request.Method, request.Path, request.Headers!["apikey"].Single(), request.Headers!["Authorization"].Single())
            .Should().Be(("DELETE", "/todos", "anon-key", "Bearer user-token"),
                "a feature's model acts on its row with the table's client options and headers, as a model from Get does");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldOnlyApplyToTheNextRequest()
    {
        this.RespondWith(200, CollectionBody);
        var table = this.Client().Table<Todo>();
        await table.GetGeoJsonAsync();
        this.RespondWith(200, "[]");
        await table.Get();
        this.server.LogEntries.Last().RequestMessage!.Headers!.ContainsKey("Accept")
            .Should().BeFalse("a later read goes back to the JSON rows representation");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldThrowUndefinedFunction_GivenPostgisIsNotInstalled()
    {
        this.RespondWith(404, """{"code":"42883","details":null,"hint":"No function matches the given name and argument types. You might need to add explicit type casts.","message":"function st_asgeojson(record) does not exist"}""");
        var act = () => this.Client().Table<Todo>().GetGeoJsonAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UndefinedFunction, "PostgREST builds GeoJSON with PostGIS's ST_AsGeoJSON");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldThrowPostgrestException_GivenABodyThatIsNotJson()
    {
        this.RespondWith(200, "<html>proxy error</html>");
        var act = () => this.Client().Table<Todo>().GetGeoJsonAsync();
        await act.Should().ThrowAsync<PostgrestException>("a success that cannot be read must not reach the caller as features");
    }

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldStopTheRequest_GivenACancelledToken()
    {
        this.RespondWith(200, CollectionBody);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => this.Client().Table<Todo>().GetGeoJsonAsync(cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token reaches the HTTP call");
    }

    [TestMethod]
    public void GetGeoJsonAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.RespondWith(200, CollectionBody);
        BlockingCaller.Completes(() => this.Client().Table<Todo>().GetGeoJsonAsync())
            .Should().BeTrue("a library call must not capture the caller's synchronization context");
    }

    private Client Client(ClientOptions? options = null) => new(this.server.Url!, options ?? new ClientOptions());

    private void RespondWith(int status, string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/geo+json").WithBody(body));
}
