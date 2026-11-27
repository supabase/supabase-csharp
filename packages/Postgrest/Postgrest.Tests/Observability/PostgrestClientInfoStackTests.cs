using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Support;
using Supabase.Core;
using Supabase.Postgrest;

namespace Postgrest.Tests.Observability;

/// <summary>
/// The <c>X-Client-Info</c> value reaches PostgREST through the local stack's gateway, read back by the
/// <c>echo_client_info</c> function (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PostgrestClientInfoStackTests
{
    private const string EchoFunction = "echo_client_info";

    [TestMethod]
    public async Task TypedRpc_ShouldReachTheServiceWithTypedApi() =>
        (await LocalStack.Client().Rpc<string>(EchoFunction))
            .Should().Be($"{Util.GetAssemblyVersion(typeof(Client))}; features=typed-api");

    [TestMethod]
    public async Task Rpc_ShouldReachTheServiceWithoutFeatures() =>
        (await LocalStack.Client().Rpc(EchoFunction, null)).Content
            .Should().Be($"\"{Util.GetAssemblyVersion(typeof(Client))}\"", "the untyped RPC call uses no SDK feature");
}
