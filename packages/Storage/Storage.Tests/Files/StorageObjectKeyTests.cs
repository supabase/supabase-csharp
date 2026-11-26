using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Files;

/// <summary>
/// Contract tests for how an object key becomes part of a request URL: each segment is escaped, dot
/// segments that stay inside the bucket resolve, and a key that leaves its bucket is rejected before
/// anything is sent.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageObjectKeyTests
{
    private const string Bucket = "bucket";

    private WireMockServer server = null!;
    private Client client = null!;
    private string localFile = null!;

    private delegate Task ObjectRequest(IStorageFileApi<FileObject> bucket, string key, string localFile);

    [TestInitialize]
    public void TestInitialize()
    {
        this.localFile = Path.GetTempFileName();
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().WithPath("/storage/v1/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("{\"signedURL\":\"/signed?token=t\",\"url\":\"/signed?token=t\"}"));
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>());
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.server.Stop();
        File.Delete(this.localFile);
    }

    private static IEnumerable<object[]> ObjectRequests() =>
        new (string Route, ObjectRequest Send)[]
        {
            ("/object/info/", (bucket, key, _) => bucket.Info(key)),
            ("/object/", (bucket, key, _) => bucket.Download(key, (TransformOptions?) null)),
            ("/object/", (bucket, key, file) => bucket.Download(key, file, (TransformOptions?) null)),
            ("/render/image/authenticated/", (bucket, key, file) => bucket.Download(key, file, new TransformOptions { Width = 10 })),
            ("/object/sign/", (bucket, key, _) => bucket.CreateSignedUrl(key, 60)),
            ("/object/upload/sign/", (bucket, key, _) => bucket.CreateUploadSignedUrl(key)),
            ("/object/", (bucket, key, _) => bucket.Upload(new byte[] { 0x1 }, key)),
            ("/object/", (bucket, key, file) => bucket.Upload(file, key)),
            ("/object/", (bucket, key, _) => bucket.Update(new byte[] { 0x1 }, key)),
            ("/object/", (bucket, key, file) => bucket.Update(file, key)),
            ("/cdn/", (bucket, key, _) => bucket.PurgeCache(key)),
        }.Select(request => new object[] { request.Route, request.Send });

    private Task Send(object request, string key) => ((ObjectRequest) request)(this.client.From(Bucket), key, this.localFile);

    private string RequestedPath() => new Uri(this.server.LogEntries.Single().RequestMessage!.AbsoluteUrl).AbsolutePath;

    [TestMethod]
    [DynamicData(nameof(ObjectRequests))]
    public async Task ObjectRequests_ShouldEncodeEachSegment_GivenAKeyWithUrlDelimiters(string route, object request)
    {
        await this.Send(request, "folder/a?b#c.png");
        this.RequestedPath().Should().Be($"/storage/v1{route}{Bucket}/folder/a%3Fb%23c.png",
            "an unescaped '?' or '#' truncates the key into a query string or fragment");
    }

    [TestMethod]
    [DynamicData(nameof(ObjectRequests))]
    public async Task ObjectRequests_ShouldResolveInsideTheBucket_GivenADotSegmentThatStaysInIt(string route, object request)
    {
        await this.Send(request, "a/../b");
        this.RequestedPath().Should().Be($"/storage/v1{route}{Bucket}/b");
    }

    [TestMethod]
    [DynamicData(nameof(ObjectRequests))]
    public async Task ObjectRequests_ShouldThrow_GivenAKeyClimbingAboveItsBucket(string route, object request) =>
        await FluentActions.Awaiting(() => this.Send(request, "../b"))
            .Should().ThrowAsync<ArgumentException>($"System.Uri would resolve the key out of {route}{Bucket}");

    [TestMethod]
    [DataRow("./a.png", "a.png")]
    [DataRow("a/..", "")]
    [DataRow("a/b/../../c", "c")]
    [DataRow("a//../b", "a/b")]
    public async Task Info_ShouldResolveInsideTheBucket_GivenDotSegmentsThatStayInIt(string key, string resolved)
    {
        await this.client.From(Bucket).Info(key);
        this.RequestedPath().Should().Be($"/storage/v1/object/info/{Bucket}/{resolved}");
    }

    [TestMethod]
    [DataRow("..")]
    [DataRow("a/../../b")]
    [DataRow("../other/a.png")]
    [DataRow("../bucket/a.png")]
    [DataRow("./../b")]
    public async Task Info_ShouldThrowNamingTheKeyParameter_GivenAKeyClimbingAboveItsBucket(string key) =>
        await FluentActions.Awaiting(() => this.client.From(Bucket).Info(key))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*cannot leave its bucket*").WithParameterName("path");

    [TestMethod]
    public async Task Info_ShouldSendNothing_GivenAKeyClimbingAboveItsBucket()
    {
        await Task.WhenAny(this.client.From(Bucket).Info("../b"));
        this.server.LogEntries.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Info_ShouldThrow_GivenADotSegmentAsTheBucket() =>
        await FluentActions.Awaiting(() => this.client.From("..").Info("a.png")).Should().ThrowAsync<ArgumentException>();
}
