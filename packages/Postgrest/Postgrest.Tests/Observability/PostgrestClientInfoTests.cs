using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Core;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Observability;

/// <summary>
/// Every Database request identifies the SDK in a single <c>X-Client-Info</c> header, or carries the value the
/// application supplied in any casing, and a request built with the typed API adds <c>features=typed-api</c>
/// (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PostgrestClientInfoTests
{
    private const string SuppliedValue = "my-lib/1.0";
    private const string TypedApi = "features=typed-api";

    private static readonly string SdkValue = Util.GetAssemblyVersion(typeof(Client));

    private WireMockServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task Get_ShouldSendTheSdkValueWithTypedApi()
    {
        await this.Client().Table<Todo>().Get();
        this.SentClientInfo().Should().Equal($"{SdkValue}; {TypedApi}");
    }

    [TestMethod]
    public async Task Delete_ShouldSendTypedApi()
    {
        await this.Client().Table<Todo>().Where(todo => todo.Id == 1).Delete();
        this.SentClientInfo().Should().Equal([$"{SdkValue}; {TypedApi}"], "every table request is built with the typed API");
    }

    [TestMethod]
    public async Task TypedRpc_ShouldSendTypedApi()
    {
        await this.Client().Rpc<List<int>>("add_one");
        this.SentClientInfo().Should().Equal([$"{SdkValue}; {TypedApi}"], "a typed RPC call is built with the typed API");
    }

    [TestMethod]
    public async Task Rpc_ShouldSendTheSdkValueWithoutFeatures()
    {
        await this.Client().Rpc("add_one", null);
        this.SentClientInfo().Should().Equal([SdkValue], "the untyped RPC call uses no SDK feature");
    }

    [TestMethod]
    public async Task Get_ShouldAddTypedApiToTheSuppliedValue_GivenClientHeaderInAnyCasing()
    {
        await this.Client(new Dictionary<string, string> { ["x-client-info"] = SuppliedValue }).Table<Todo>().Get();
        this.SentClientInfo().Should().Equal([$"{SuppliedValue}; {TypedApi}"], "an application-supplied value replaces the SDK's and still gets the features");
    }

    [TestMethod]
    public async Task Get_ShouldAddTypedApiToTheSuppliedValue_GivenDynamicHeaders()
    {
        var client = this.Client();
        client.GetHeaders = () => new Dictionary<string, string> { ["X-Client-Info"] = SuppliedValue };
        await client.Table<Todo>().Get();
        this.SentClientInfo().Should().Equal($"{SuppliedValue}; {TypedApi}");
    }

    private Client Client(Dictionary<string, string>? headers = null) =>
        new(this.server.Url!, new ClientOptions { Headers = headers ?? new Dictionary<string, string>() });

    private IEnumerable<string> SentClientInfo() =>
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Client-Info"].ToList();
}
