using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.ClientIdentification;

namespace Core.Tests.ClientIdentification;

/// <summary>
/// The <c>X-Client-Info</c> header an outgoing request ends up with: the SDK's value unless the application supplied
/// one under any casing, always a single header, with the request's features merged in (client_info spec,
/// Application-supplied value and Features).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientInfoHeaderTests
{
    private const string SdkHeaderValue = "supabase-csharp/8.14.0; runtime=dotnet";

    private static readonly ClientInfo Sdk = new(new ProductToken("supabase-csharp", "8.14.0"), [new ClientInfoParameter("runtime", "dotnet")]);

    [TestMethod]
    public void Inject_ShouldSendTheSdkValue_GivenNoSuppliedValue() =>
        Injected(Headers()).Should().Equal(SdkHeaderValue);

    [TestMethod]
    public void Inject_ShouldKeepTheSuppliedValue_GivenApplicationSetTheHeaderInAnyCasing() =>
        Injected(Headers(("x-client-info", "my-lib/1.0"))).Should().Equal(["my-lib/1.0"], "an application-supplied value, matched case-insensitively, replaces the SDK's");

    [TestMethod]
    public void Inject_ShouldSendASingleHeader_GivenTwoSuppliedCasings() =>
        Injected(Headers(("x-client-info", "my-lib/1.0"), ("X-Client-Info", "other/2.0"))).Should().Equal("my-lib/1.0");

    [TestMethod]
    public void Inject_ShouldAppendTheFeatures_GivenTypedApi() =>
        Injected(Headers(), SdkFeatures.TypedApi).Should().Equal($"{SdkHeaderValue}; features=typed-api");

    [TestMethod]
    public void Inject_ShouldAddTheFeaturesToTheSuppliedValue_GivenTypedApi() =>
        Injected(Headers(("X-Client-Info", "my-lib/1.0")), SdkFeatures.TypedApi).Should().Equal(["my-lib/1.0; features=typed-api"],
            "the SDK still adds the features parameter to an application-supplied value");

    [TestMethod]
    public void Inject_ShouldMergeSortAndDeduplicateFeatures_GivenSuppliedFeatures() =>
        Injected(Headers(("X-Client-Info", "my-lib/1.0; Features= zeta,typed-api ,, alpha; via=x")), SdkFeatures.TypedApi)
            .Should().Equal(["my-lib/1.0; via=x; features=alpha,typed-api,zeta"], "both lists merge into one parameter, sorted and without duplicates");

    [TestMethod]
    public void Inject_ShouldDropEmptySegments_GivenFeaturesToMerge() =>
        Injected(Headers(("X-Client-Info", "my-lib/1.0;; via=x ;")), SdkFeatures.TypedApi).Should().Equal("my-lib/1.0; via=x; features=typed-api");

    [TestMethod]
    public void Inject_ShouldLeaveTheSuppliedValueUntouched_GivenNoFeatures() =>
        Injected(Headers(("X-Client-Info", "my-lib/1.0;via=x; features=alpha"))).Should().Equal("my-lib/1.0;via=x; features=alpha");

    [TestMethod]
    public void Inject_ShouldAddTheSdkValue_GivenHandshakeHeadersWithoutIt()
    {
        var headers = new Dictionary<string, string> { ["apikey"] = "key" };
        ClientInfoHeader.Inject(headers, Sdk);
        headers.Should().Contain("X-Client-Info", SdkHeaderValue);
    }

    [TestMethod]
    public void Inject_ShouldKeepTheSuppliedValue_GivenHandshakeHeadersInAnyCasing()
    {
        var headers = new Dictionary<string, string> { ["x-client-info"] = "my-lib/1.0" };
        ClientInfoHeader.Inject(headers, Sdk);
        headers.Should().Equal(new Dictionary<string, string> { ["x-client-info"] = "my-lib/1.0" });
    }

    private static HttpHeaders Headers(params (string Name, string Value)[] supplied)
    {
        var headers = new HttpRequestMessage().Headers;
        foreach (var header in supplied)
            headers.TryAddWithoutValidation(header.Name, header.Value);
        return headers;
    }

    private static IEnumerable<string> Injected(HttpHeaders headers)
    {
        ClientInfoHeader.Inject(headers, Sdk);
        return headers.GetValues(ClientInfoHeader.Name);
    }

    private static IEnumerable<string> Injected(HttpHeaders headers, SdkFeatures features)
    {
        ClientInfoHeader.Inject(headers, Sdk, features);
        return headers.GetValues(ClientInfoHeader.Name);
    }
}
