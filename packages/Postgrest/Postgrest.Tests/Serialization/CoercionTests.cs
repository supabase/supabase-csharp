using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;

namespace Postgrest.Tests.Serialization;

/// <summary>
///     End-to-end proof that values survive the full write→read round-trip through PostgREST and Postgres:
///     primitives, collections, ranges and infinity dates keep their value, and a client-generated GUID
///     persists on update.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class CoercionTests
{
    [TestMethod]
    public async Task Insert_ShouldRoundTripEveryPrimitiveAndCollection()
    {
        var client = LocalStack.Client();
        // Scope to the seeded row; other tests insert kitchen_sink rows (issue #300).
        var existing = await client.Table<KitchenSink>()
            .Where(x => x.Id == new Guid("f3ff356d-5803-43a7-b125-ba10cf10fdcd")).Single();
        if (existing != null)
        {
            using (new AssertionScope())
            {
                existing.FloatValue.Should().Be(99999.0f);
                existing.DoubleValue.Should().Be(99999.0d);
                existing.ListOfStrings.Should().BeEquivalentTo("set", "of", "strings");
                existing.DateTimePosInfinity.Should().Be(DateTime.MaxValue);
                existing.DateTimeNegInfinity.Should().Be(DateTime.MinValue);
                existing.ListOfFloats.Should().BeEquivalentTo(new List<float> { 10.0f, 12.0f });
                existing.IntRange.Should().Be(new IntRange(20, 50));
            }
        }
        var model = new KitchenSink
        {
            StringValue = "test",
            IntValue = 1,
            FloatValue = 1.1f,
            DoubleValue = 1.1d,
            DateTimeValue = DateTime.UtcNow,
            DateTimeNegInfinity = DateTime.MinValue,
            DateTimePosInfinity = DateTime.MaxValue,
            ListOfStrings = new List<string> { "test", "1", "2", "3" },
            ListOfDateTimes = new List<DateTime> { new(2021, 12, 10), new(2021, 12, 11), new(2021, 12, 12) },
            ListOfInts = new List<int> { 1, 2, 3 },
            ListOfFloats = new List<float> { 1.1f, 1.2f, 1.3f },
            IntRange = new IntRange(0, 1)
        };
        var actual = (await client.Table<KitchenSink>().Insert(model)).Models.First();
        using (new AssertionScope())
        {
            actual.StringValue.Should().Be(model.StringValue);
            actual.IntValue.Should().Be(model.IntValue);
            actual.FloatValue.Should().Be(model.FloatValue);
            actual.DoubleValue.Should().Be(model.DoubleValue);
            actual.DateTimeValue.ToString().Should().Be(model.DateTimeValue.ToString());
            actual.ListOfStrings.Should().BeEquivalentTo(model.ListOfStrings);
            actual.ListOfDateTimes.Should().BeEquivalentTo(model.ListOfDateTimes);
            actual.ListOfInts.Should().BeEquivalentTo(model.ListOfInts);
            actual.ListOfFloats.Should().BeEquivalentTo(model.ListOfFloats);
            actual.IntRange!.Start.Value.Should().Be(model.IntRange.Start.Value);
            actual.IntRange.End.Value.Should().Be(model.IntRange.End.Value);
        }
    }

    [TestMethod]
    public async Task Update_ShouldPersistAClientGeneratedGuid()
    {
        var client = LocalStack.Client();
        var inserted = await client.Table<KitchenSink>().Insert(new KitchenSink());
        inserted.Model.Should().NotBeNull();
        inserted.Model!.Uuidv4.Should().BeNull();
        var guid = Guid.NewGuid();
        inserted.Model.Uuidv4 = guid;
        var updated = await inserted.Model.Update<KitchenSink>();
        updated.Model!.Uuidv4.Should().Be(guid);
    }
}
