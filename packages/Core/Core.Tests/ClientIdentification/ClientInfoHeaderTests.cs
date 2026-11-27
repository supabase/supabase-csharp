using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;
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
    private static readonly Type SdkClient = typeof(Util);

    private static readonly string SdkHeaderValue = ClientInfo.Of(SdkClient).HeaderValue;

    [TestMethod]
    public void AddClientInfo_ShouldSendTheSdkValue_GivenNoSuppliedValue() =>
        Injected(Headers()).Should().Equal(SdkHeaderValue);

    [TestMethod]
    public void AddClientInfo_ShouldKeepTheSuppliedValue_GivenApplicationSetTheHeaderInAnyCasing() =>
        Injected(Headers(new SuppliedHeader("x-client-info", "my-lib/1.0"))).Should().Equal(["my-lib/1.0"], "an application-supplied value, matched case-insensitively, replaces the SDK's");

    [TestMethod]
    public void AddClientInfo_ShouldSendASingleHeader_GivenTwoSuppliedCasings() =>
        Injected(Headers(new SuppliedHeader("x-client-info", "my-lib/1.0"), new SuppliedHeader("X-Client-Info", "other/2.0"))).Should().Equal("my-lib/1.0");

    [TestMethod]
    public void AddClientInfo_ShouldAppendTheFeatures_GivenTypedApi() =>
        Injected(Headers(), SdkFeatures.TypedApi).Should().Equal($"{SdkHeaderValue}; features=typed-api");

    [TestMethod]
    public void AddClientInfo_ShouldAddTheFeaturesToTheSuppliedValue_GivenTypedApi() =>
        Injected(Headers(new SuppliedHeader("X-Client-Info", "my-lib/1.0")), SdkFeatures.TypedApi).Should().Equal(["my-lib/1.0; features=typed-api"],
            "the SDK still adds the features parameter to an application-supplied value");

    [TestMethod]
    public void AddClientInfo_ShouldMergeSortAndDeduplicateFeatures_GivenSuppliedFeatures() =>
        Injected(Headers(new SuppliedHeader("X-Client-Info", "my-lib/1.0; Features= zeta,typed-api ,, alpha; via=x")), SdkFeatures.TypedApi)
            .Should().Equal(["my-lib/1.0; via=x; features=alpha,typed-api,zeta"], "both lists merge into one parameter, sorted and without duplicates");

    [TestMethod]
    public void AddClientInfo_ShouldDropEmptySegments_GivenFeaturesToMerge() =>
        Injected(Headers(new SuppliedHeader("X-Client-Info", "my-lib/1.0;; via=x ;")), SdkFeatures.TypedApi).Should().Equal("my-lib/1.0; via=x; features=typed-api");

    [TestMethod]
    public void AddClientInfo_ShouldLeaveTheSuppliedValueUntouched_GivenNoFeatures() =>
        Injected(Headers(new SuppliedHeader("X-Client-Info", "my-lib/1.0;via=x; features=alpha"))).Should().Equal("my-lib/1.0;via=x; features=alpha");

    [TestMethod]
    public void Of_ShouldReturnTheSdkValueOfTheClientAssembly() =>
        ClientInfoHeader.Of(SdkClient).Should().Be(SdkHeaderValue, "a caller that keeps its headers in its own collection adds this value itself");

    private static HttpHeaders Headers(params SuppliedHeader[] supplied)
    {
        var headers = new HttpRequestMessage().Headers;
        foreach (var header in supplied)
            headers.TryAddWithoutValidation(header.Name, header.Value);
        return headers;
    }

    private static IEnumerable<string> Injected(HttpHeaders headers)
    {
        headers.AddClientInfo(SdkClient);
        return headers.GetValues(ClientInfoHeader.Name);
    }

    private static IEnumerable<string> Injected(HttpHeaders headers, SdkFeatures features)
    {
        headers.AddClientInfo(SdkClient, features);
        return headers.GetValues(ClientInfoHeader.Name);
    }

    private sealed record SuppliedHeader(string Name, string Value);
}
