using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;

namespace Postgrest.Tests.Serialization;

/// <summary>
///     The serializer's int array contract (issue #395): writes are a plain JSON array, and a malformed
///     Postgres literal surfaces as a <see cref="JsonException" />.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IntArraySerializationTests
{
    private static KitchenSink Deserialize(string json) =>
        JsonSerializer.Deserialize<KitchenSink>(json, Client.SerializerSettings())!;

    private static string Serialize(KitchenSink model) =>
        JsonSerializer.Serialize(model, Client.SerializerSettings());

    [TestMethod]
    public void Write_ShouldEmitAJsonArray() =>
        Serialize(new KitchenSink { ListOfInts = new List<int> { -1, 2 } }).Should().Contain("\"list_of_ints\":[-1,2]");

    [TestMethod]
    public void Read_ShouldAcceptAPostgresLiteral() =>
        Deserialize("{\"list_of_ints\":\"{1,-2,3}\"}").ListOfInts.Should().Equal(1, -2, 3);

    [TestMethod]
    public void Read_ShouldReturnEmpty_GivenAnEmptyLiteral() =>
        Deserialize("{\"list_of_ints\":\"{}\"}").ListOfInts.Should().BeEmpty();

    [TestMethod]
    [DataRow("{1,NULL,3}")]
    [DataRow("{1,,3}")]
    public void Read_ShouldThrowJsonException_GivenAMalformedLiteral(string literal)
    {
        var act = () => Deserialize($"{{\"list_of_ints\":\"{literal}\"}}");
        act.Should().Throw<JsonException>().WithMessage($"*{literal}*");
    }
}
