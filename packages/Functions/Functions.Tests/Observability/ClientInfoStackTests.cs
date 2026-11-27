using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;
using Supabase.Functions;
using static Supabase.Functions.Client;

namespace Functions.Tests.Observability;

/// <summary>
/// The <c>X-Client-Info</c> value reaches a function running on the local Supabase stack, which reports the header it
/// received: the SDK's own value, or the one the application supplied (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ClientInfoStackTests
{
    private const string EchoFunction = "echo-headers";

    [TestMethod]
    public async Task Invoke_ShouldDeliverTheSdkValueToTheFunction() =>
        ClientInfoReceivedBy(await Functions().Invoke(EchoFunction, LocalStackToken.Issue()))
            .Should().Be(Util.GetAssemblyVersion(typeof(Client)));

    [TestMethod]
    public async Task Invoke_ShouldDeliverTheSuppliedValueToTheFunction_GivenOptionHeader() =>
        ClientInfoReceivedBy(await Functions().Invoke(EchoFunction, LocalStackToken.Issue(), new InvokeFunctionOptions
        {
            Headers = new Dictionary<string, string> { ["x-client-info"] = "my-lib/1.0" }
        })).Should().Be("my-lib/1.0", "an application-supplied value replaces the SDK's");

    private static Client Functions() => new("http://localhost:54321/functions/v1");

    private static string? ClientInfoReceivedBy(string echoed) =>
        JsonDocument.Parse(echoed).RootElement.GetProperty("clientInfo").GetString();
}
