using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime.Converters;

namespace Realtime.Tests.Serialization;

/// <summary>
///     WALRUS delivers Postgres array columns as strings in either <c>{1,2,3}</c> (curly) or <c>[1,2,3]</c>
///     (bracket) form; <see cref="IntArrayConverter" /> and <see cref="StringArrayConverter" /> coerce both
///     back into a typed list. These pin that coercion, including the empty and whitespace edges.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ArrayConverterTests
{
    private class ArrayModel
    {
        [JsonPropertyName("intArray")] public List<int> IntArray { get; set; } = new();
        [JsonPropertyName("stringArray")] public List<string> StringArray { get; set; } = new();
        [JsonPropertyName("nestedIntArray")] public List<List<int>> NestedIntArray { get; set; } = new();
        [JsonPropertyName("nestedStringArray")] public List<List<string>> NestedStringArray { get; set; } = new();
    }

    private static ArrayModel Coerce(string json) =>
        JsonSerializer.Deserialize<ArrayModel>(json, Wire.Settings())!;

    [TestMethod]
    public void ContractResolver_ShouldCoerceArrayColumns_GivenJsonArrays()
    {
        var parsed = Coerce("{\"intArray\":[9999,99,99999], \"stringArray\": [\"testing\",\"1\",\"2\"]}");
        parsed.IntArray.Should().Equal(9999, 99, 99999);
        parsed.StringArray.Should().Equal("testing", "1", "2");
    }

    [TestMethod]
    public void ContractResolver_ShouldCoerceArrayColumns_GivenWalrusEncodedStrings()
    {
        // WALRUS delivers array columns as a single Postgres-array string; the converter (not default
        // deserialization) is what turns it back into a list, so this exercises the converter is engaged.
        var parsed = Coerce("{\"intArray\":\"{9999,99,99999}\", \"stringArray\":\"{testing,1,2}\"}");
        parsed.IntArray.Should().Equal(9999, 99, 99999);
        parsed.StringArray.Should().Equal("testing", "1", "2");
    }

    [TestMethod]
    public void IntArrayParse_ShouldReadCurlyForm() => IntArrayConverter.Parse("{1,2,3}").Should().Equal(1, 2, 3);

    [TestMethod]
    public void IntArrayParse_ShouldReadBracketForm_GivenWhitespace()
    {
        IntArrayConverter.Parse("[1,2,3]").Should().Equal(1, 2, 3);
        IntArrayConverter.Parse("[99, 999, 9999, 999999]").Should().Equal(99, 999, 9999, 999999);
    }

    [TestMethod]
    public void IntArrayParse_ShouldReturnEmpty_GivenEmptyBraces() => IntArrayConverter.Parse("{}").Should().BeEmpty();

    [TestMethod]
    public void StringArrayParse_ShouldReadBothForms()
    {
        StringArrayConverter.Parse("{a,b,c}").Should().Equal("a", "b", "c");
        StringArrayConverter.Parse("[a,b,c]").Should().Equal("a", "b", "c");
    }

    [TestMethod]
    public void StringArrayParse_ShouldHonorQuotesAndEscapes() =>
        StringArrayConverter.Parse("""{"a,b","c\"d","e\\f",""}""")
            .Should().Equal("a,b", "c\"d", "e\\f", "");

    [TestMethod]
    public void StringArrayParse_ShouldKeepUnicodeSpaces() =>
        StringArrayConverter.Parse("{a\u00A0,\u00A0,NULL\u00A0}").Should().Equal("a\u00A0", "\u00A0", "NULL\u00A0");

    [TestMethod]
    public void StringArrayParse_ShouldReadNullElement_GivenUnquotedNull() =>
        StringArrayConverter.Parse("""{NULL,"NULL"}""")
            .Should().Equal(new[] { null!, "NULL" }, "a quoted NULL is text, not the null element");

    [TestMethod]
    public void StringArrayParse_ShouldIgnoreWhitespace_GivenUnquotedElements() =>
        StringArrayConverter.Parse(" {a , b} ").Should().Equal("a", "b");

    [TestMethod]
    public void StringArrayParse_ShouldReturnEmpty_GivenWhitespaceOnlyBraces() =>
        StringArrayConverter.Parse("{ }").Should().BeEmpty();

    [TestMethod]
    [DataRow("{a,}")]
    [DataRow("{a}b")]
    [DataRow("""{"a}""")]
    [DataRow("""{a"b}""")]
    [DataRow("{a{b}")]
    [DataRow("{a,,b}")]
    [DataRow("abc")]
    public void StringArrayRead_ShouldReturnNull_GivenMalformedLiteral(string literal) =>
        Coerce(JsonSerializer.Serialize(new { stringArray = literal })).StringArray.Should().BeNull();

    [TestMethod]
    [DataRow("{1,}")]
    [DataRow("{1,NULL}")]
    [DataRow("{{1,2},{3,4}}")]
    [DataRow("[0:1]={1,2}")]
    public void IntArrayRead_ShouldReturnNull_GivenUnreadableLiteral(string literal) =>
        Coerce(JsonSerializer.Serialize(new { intArray = literal })).IntArray.Should().BeNull();

    [TestMethod]
    public void NestedIntArrayRead_ShouldKeepShape_GivenNestedLiteral() =>
        Coerce("""{"nestedIntArray":"{{1,2},{3,4}}"}""").NestedIntArray
            .Should().BeEquivalentTo(new[] { new[] { 1, 2 }, new[] { 3, 4 } }, options => options.WithStrictOrdering());

    [TestMethod]
    public void NestedStringArrayRead_ShouldKeepShape_GivenQuotedAndNullElements() =>
        Coerce("""{"nestedStringArray":"{{\"a,b\",NULL},{c}}"}""").NestedStringArray
            .Should().BeEquivalentTo(new[] { new[] { "a,b", null }, new[] { "c" } }, options => options.WithStrictOrdering());

    [TestMethod]
    public void NestedIntArrayRead_ShouldKeepShape_GivenJsonArrays() =>
        Coerce("""{"nestedIntArray":[[1,2],[3,4]]}""").NestedIntArray
            .Should().BeEquivalentTo(new[] { new[] { 1, 2 }, new[] { 3, 4 } }, options => options.WithStrictOrdering());

    [TestMethod]
    [DataRow("{1,2,3}")]
    [DataRow("{{1,2},3}")]
    [DataRow("{{1,2}")]
    public void NestedIntArrayRead_ShouldReturnNull_GivenUnreadableLiteral(string literal) =>
        Coerce(JsonSerializer.Serialize(new { nestedIntArray = literal })).NestedIntArray.Should().BeNull();

    [TestMethod]
    public void NestedIntArrayRead_ShouldReturnNullAndKeepReading_GivenAJsonArrayWithABadElement() =>
        Coerce("""{"nestedIntArray":[[1,2],["x",4]],"stringArray":"{ok}"}""")
            .Should().BeEquivalentTo(new { NestedIntArray = (List<List<int>>?) null, StringArray = new[] { "ok" } });

    [TestMethod]
    public void NestedIntArrayRead_ShouldReturnNullAndKeepReading_GivenAJsonObject() =>
        Coerce("""{"nestedIntArray":{},"stringArray":"{ok}"}""")
            .Should().BeEquivalentTo(new { NestedIntArray = (List<List<int>>?) null, StringArray = new[] { "ok" } });

    [TestMethod]
    [DataRow("""{"intArray":[1,"x"],"stringArray":"{ok}"}""")]
    [DataRow("""{"intArray":{},"stringArray":"{ok}"}""")]
    public void IntArrayRead_ShouldReturnNullAndKeepReading_GivenABadJsonValue(string json) =>
        Coerce(json).Should().BeEquivalentTo(new { IntArray = (List<int>?) null, StringArray = new[] { "ok" } });

    [TestMethod]
    [DataRow("""{"stringArray":["a",1],"intArray":"{1}"}""")]
    [DataRow("""{"stringArray":{},"intArray":"{1}"}""")]
    public void StringArrayRead_ShouldReturnNullAndKeepReading_GivenABadJsonValue(string json) =>
        Coerce(json).Should().BeEquivalentTo(new { StringArray = (List<string>?) null, IntArray = new[] { 1 } });

    [TestMethod]
    public void IntArrayRead_ShouldReturnNull_GivenDeeplyNestedBraces() =>
        Coerce(JsonSerializer.Serialize(new { intArray = new string('{', 100_000) })).IntArray.Should().BeNull();

    [TestMethod]
    public void NestedIntArrayWrite_ShouldEmitJsonArrays()
    {
        var model = new ArrayModel { NestedIntArray = new() { new() { 1, 2 }, new() { 3, 4 } } };
        var json = JsonNode.Parse(JsonSerializer.Serialize(model, Wire.Settings()))!;
        json["nestedIntArray"]!.ToJsonString().Should().Be("[[1,2],[3,4]]");
    }
}
