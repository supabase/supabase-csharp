using System;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>Covers <see cref="JsonBodyReader"/>: deserializing a response body or throwing the caller's exception.</summary>
[TestClass]
[TestCategory("Unit")]
public class JsonBodyReaderTests
{
    [TestMethod]
    public void Deserialize_ShouldReturnTheValue() =>
        JsonBodyReader.Deserialize<int[]>("[1,2]", new JsonSerializerOptions(), e => e).Should().Equal(1, 2);

    [TestMethod]
    public void Deserialize_ShouldWrapJsonException()
    {
        var act = () => JsonBodyReader.Deserialize<int[]>("<html>", new JsonSerializerOptions(), e => new InvalidOperationException("wrapped", e));
        act.Should().Throw<InvalidOperationException>().WithInnerException<JsonException>();
    }
}
