using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.Buckets;

[TestClass]
[TestCategory("Unit")]
public class BucketFileSizeLimitTests
{
    [TestMethod]
    public void Deserialize_ShouldReadNumericFileSizeLimit()
    {
        var bucket = JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":1048576}",
            Supabase.Storage.Helpers.SerializerOptions);

#pragma warning disable CS0618
        bucket!.FileSizeLimit.Should().Be("1048576");
#pragma warning restore CS0618
        bucket.FileSizeLimitInBytes.Should().Be(1048576L);
    }

    [TestMethod]
    public void Deserialize_ShouldReadFileSizeLimitAboveInt32Range()
    {
        var bucket = JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":3000000000}",
            Supabase.Storage.Helpers.SerializerOptions);

        bucket!.FileSizeLimitInBytes.Should().Be(3000000000L);
#pragma warning disable CS0618
        bucket.FileSizeLimit.Should().Be("3000000000");
#pragma warning restore CS0618
    }

    [TestMethod]
    public void Deserialize_ShouldReadListWithNumericAndNullFileSizeLimits()
    {
        var buckets = JsonSerializer.Deserialize<List<Bucket>>(
            "[{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":1048576},{\"id\":\"b\",\"name\":\"b\",\"file_size_limit\":null}]",
            Supabase.Storage.Helpers.SerializerOptions);

        buckets.Should().NotBeNull().And.HaveCount(2);
        buckets![0].FileSizeLimitInBytes.Should().Be(1048576L);
        buckets[1].FileSizeLimitInBytes.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_ShouldReadNullFileSizeLimit()
    {
        var bucket = JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":null}",
            Supabase.Storage.Helpers.SerializerOptions);

#pragma warning disable CS0618
        bucket!.FileSizeLimit.Should().BeNull();
#pragma warning restore CS0618
        bucket.FileSizeLimitInBytes.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_ShouldReadMissingFileSizeLimit()
    {
        var bucket = JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\"}",
            Supabase.Storage.Helpers.SerializerOptions);

#pragma warning disable CS0618
        bucket!.FileSizeLimit.Should().BeNull();
#pragma warning restore CS0618
        bucket.FileSizeLimitInBytes.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_ShouldReadQuotedFileSizeLimit()
    {
        var bucket = JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":\"1048576\"}",
            Supabase.Storage.Helpers.SerializerOptions);

        bucket!.FileSizeLimitInBytes.Should().Be(1048576L);
    }

    [TestMethod]
    public void Deserialize_ShouldRejectUnsupportedFileSizeLimitToken()
    {
        var act = () => JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":{}}",
            Supabase.Storage.Helpers.SerializerOptions);

        act.Should().Throw<JsonException>();
    }

    [TestMethod]
    public void Deserialize_ShouldRejectNonIntegerFileSizeLimit()
    {
        var act = () => JsonSerializer.Deserialize<Bucket>(
            "{\"id\":\"a\",\"name\":\"a\",\"file_size_limit\":1048576.5}",
            Supabase.Storage.Helpers.SerializerOptions);

        act.Should().Throw<JsonException>();
    }

    [TestMethod]
    public void ObsoleteSetter_ShouldMapNumericStringToBytes()
    {
        var bucket = new Bucket();
#pragma warning disable CS0618
        bucket.FileSizeLimit = "1048576";
#pragma warning restore CS0618

        bucket.FileSizeLimitInBytes.Should().Be(1048576L);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("50mb")]
    [DataRow("")]
    public void ObsoleteSetter_ShouldMapNonNumericStringToNull(string value)
    {
        var bucket = new Bucket();
#pragma warning disable CS0618
        bucket.FileSizeLimit = value;
#pragma warning restore CS0618

        bucket.FileSizeLimitInBytes.Should().BeNull();
    }

    [TestMethod]
    public void ObsoleteSetter_ShouldMapNullToNull()
    {
        var bucket = new Bucket();
#pragma warning disable CS0618
        bucket.FileSizeLimit = null;
#pragma warning restore CS0618

        bucket.FileSizeLimitInBytes.Should().BeNull();
    }

    [TestMethod]
    public void ObsoleteGetter_ShouldReturnNullWhenBytesNull()
    {
        var bucket = new Bucket { FileSizeLimitInBytes = null };
#pragma warning disable CS0618
        bucket.FileSizeLimit.Should().BeNull();
#pragma warning restore CS0618
    }

    [TestMethod]
    public void ObsoleteGetter_ShouldUseInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var bucket = new Bucket { FileSizeLimitInBytes = 1048576 };
#pragma warning disable CS0618
            bucket.FileSizeLimit.Should().Be("1048576");
#pragma warning restore CS0618
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void Serialize_ShouldWriteFileSizeLimitAsNumber()
    {
        var bucket = new Bucket { FileSizeLimitInBytes = 1048576 };

        var json = JsonSerializer.Serialize(bucket, Supabase.Storage.Helpers.SerializerOptions);

        json.Should().Contain("\"file_size_limit\":1048576");
    }

    [TestMethod]
    public void Serialize_ShouldNotWriteObsoletePropertyTwice()
    {
        var bucket = new Bucket { FileSizeLimitInBytes = 1048576 };

        var json = JsonSerializer.Serialize(bucket, Supabase.Storage.Helpers.SerializerOptions);

        json.Split(new[] { "\"file_size_limit\"" }, StringSplitOptions.None).Length.Should().Be(2);
    }

    [TestMethod]
    public void Serialize_ShouldRoundTripStably()
    {
        var bucket = new Bucket { Id = "a", Name = "a", FileSizeLimitInBytes = 1048576 };

        var serialized = JsonSerializer.Serialize(bucket, Supabase.Storage.Helpers.SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<Bucket>(serialized, Supabase.Storage.Helpers.SerializerOptions);
        var serializedAgain = JsonSerializer.Serialize(deserialized, Supabase.Storage.Helpers.SerializerOptions);

        serializedAgain.Should().Be(serialized);
    }
}
