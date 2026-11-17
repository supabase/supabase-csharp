using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Supabase.Tests.CustomHttpClient;

/// <summary>
/// The client in <see cref="SupabaseOptions.HttpClient"/> reaches Storage's request, upload and download paths unless
/// <see cref="Storage.ClientOptions"/> names its own (client.request_configuration.custom_http_client).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class StorageHttpClientSharingTests
{
    private static Supabase.Client ClientWith(SupabaseOptions options) => new("http://localhost", "test-key", options);

    [TestMethod]
    public void Client_ShouldSendEveryStoragePathThroughTheSharedClient_GivenNoStorageClients()
    {
        using var shared = new HttpClient();
        var storage = ClientWith(new SupabaseOptions { HttpClient = shared }).Storage.Options;
        using (new AssertionScope())
        {
            storage.HttpRequestClient.Should().BeSameAs(shared, "requests go through the caller's handler chain");
            storage.HttpUploadClient.Should().BeSameAs(shared, "uploads go through the caller's handler chain");
            storage.HttpDownloadClient.Should().BeSameAs(shared, "downloads go through the caller's handler chain");
        }
    }

    [TestMethod]
    public void Client_ShouldKeepTheExplicitStorageClients_GivenOneForEachPath()
    {
        using var shared = new HttpClient();
        using var request = new HttpClient();
        using var upload = new HttpClient();
        using var download = new HttpClient();
        var storage = ClientWith(new SupabaseOptions
        {
            HttpClient = shared,
            StorageClientOptions = new Storage.ClientOptions
            {
                HttpRequestClient = request, HttpUploadClient = upload, HttpDownloadClient = download
            }
        }).Storage.Options;
        using (new AssertionScope())
        {
            storage.HttpRequestClient.Should().BeSameAs(request, "an explicit Storage client wins over the shared one");
            storage.HttpUploadClient.Should().BeSameAs(upload, "an explicit Storage client wins over the shared one");
            storage.HttpDownloadClient.Should().BeSameAs(download, "an explicit Storage client wins over the shared one");
        }
    }

    [TestMethod]
    public void Client_ShouldSendTheOtherPathsThroughTheSharedClient_GivenOneExplicitStorageClient()
    {
        using var shared = new HttpClient();
        using var upload = new HttpClient();
        var storage = ClientWith(new SupabaseOptions
        {
            HttpClient = shared,
            StorageClientOptions = new Storage.ClientOptions { HttpUploadClient = upload }
        }).Storage.Options;
        using (new AssertionScope())
        {
            storage.HttpRequestClient.Should().BeSameAs(shared);
            storage.HttpDownloadClient.Should().BeSameAs(shared);
        }
    }

    [TestMethod]
    public void Client_ShouldPreferTheThirdPartyToken_GivenACallerTokenCallbackAndASharedClient()
    {
        using var shared = new HttpClient();
        Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<string?>> thirdParty = _ => System.Threading.Tasks.Task.FromResult<string?>("third-party");
        var storage = ClientWith(new SupabaseOptions
        {
            HttpClient = shared,
            ThirdPartyAccessToken = thirdParty,
            StorageClientOptions = new Storage.ClientOptions { RetrieveAccessToken = _ => System.Threading.Tasks.Task.FromResult<string?>("caller") }
        }).Storage.Options;
        storage.RetrieveAccessToken.Should().BeSameAs(thirdParty, "the third-party provider supplies the token for every Storage request");
    }

    [TestMethod]
    public void Client_ShouldNotChangeTheCallerStorageOptions_GivenASharedClient()
    {
        using var shared = new HttpClient();
        var callerOptions = new Storage.ClientOptions();
        ClientWith(new SupabaseOptions { HttpClient = shared, StorageClientOptions = callerOptions });
        using (new AssertionScope())
        {
            callerOptions.HttpRequestClient.Should().BeNull("the caller's options object is not modified");
            callerOptions.HttpUploadClient.Should().BeNull();
            callerOptions.HttpDownloadClient.Should().BeNull();
        }
    }

    [TestMethod]
    public void Client_ShouldKeepTheOtherStorageSettings_GivenASharedClient()
    {
        using var shared = new HttpClient();
        var proxy = new WebProxy("http://proxy.example.com");
        var retry = new RetryOptions { MaxRetries = 4 };
        var storage = ClientWith(new SupabaseOptions
        {
            HttpClient = shared,
            StorageClientOptions = new Storage.ClientOptions
            {
                HttpUploadTimeout = TimeSpan.FromSeconds(11),
                HttpDownloadTimeout = TimeSpan.FromSeconds(12),
                HttpRequestTimeout = TimeSpan.FromSeconds(13),
                Proxy = proxy,
                Retry = retry,
                RetrieveAccessToken = _ => System.Threading.Tasks.Task.FromResult<string?>("token"),
            }
        }).Storage.Options;
        using (new AssertionScope())
        {
            storage.HttpUploadTimeout.Should().Be(TimeSpan.FromSeconds(11));
            storage.HttpDownloadTimeout.Should().Be(TimeSpan.FromSeconds(12));
            storage.HttpRequestTimeout.Should().Be(TimeSpan.FromSeconds(13));
            storage.Proxy.Should().BeSameAs(proxy);
            storage.Retry.Should().BeSameAs(retry);
            storage.RetrieveAccessToken.Should().NotBeNull("an access token callback set by the caller survives the copy");
        }
    }

    [TestMethod]
    public void Client_ShouldUseTheCallerStorageOptionsAsIs_GivenNoSharedClient()
    {
        var callerOptions = new Storage.ClientOptions();
        ClientWith(new SupabaseOptions { StorageClientOptions = callerOptions }).Storage.Options.Should().BeSameAs(callerOptions);
    }
}
