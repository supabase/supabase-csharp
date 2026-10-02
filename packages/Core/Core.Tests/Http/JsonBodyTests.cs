using System;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>Covers <see cref="JsonBody"/>: deserializing a response body or throwing the caller's exception.</summary>
[TestClass]
[TestCategory("Unit")]
public class JsonBodyTests
{
    [TestMethod]
    public void Deserialize_ShouldReturnTheValue() =>
        JsonBody.Deserialize<int[]>("[1,2]", new JsonSerializerOptions(), e => e).Should().Equal(1, 2);

    [TestMethod]
    public void Deserialize_ShouldWrapJsonException()
    {
        var act = () => JsonBody.Deserialize<int[]>("<html>", new JsonSerializerOptions(), e => new InvalidOperationException("wrapped", e));

        act.Should().Throw<InvalidOperationException>().WithInnerException<JsonException>();
    }
}
