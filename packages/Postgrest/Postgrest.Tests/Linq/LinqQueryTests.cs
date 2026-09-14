using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Linq;

/// <summary>
///     End-to-end proof that the LINQ query surface — <c>Select</c>, <c>Where</c>, <c>Not</c>, <c>Or</c>,
///     <c>Set</c>, <c>OnConflict</c>, <c>Columns</c> — drives the same rows against a live PostgREST as the
///     hermetic clause-translation tests describe.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class LinqQueryTests
{
    [TestMethod]
    public async Task Select_ShouldReturnOnlyTheProjectedColumns()
    {
        var client = LocalStack.Client();
        var idOnly = await client.Table<Movie>().Select(x => new object[] { x.Id }).Get();
        idOnly.Models.First().Name.Should().BeNull();
        var idAndName = await client.Table<Movie>().Select(x => new object[] { x.Id, x.Name! }).Get();
        idAndName.Models.First().Name.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Where_ShouldSelectRowsMatchingTheTranslatedPredicate()
    {
        var client = LocalStack.Client();
        (await client.Table<Movie>().Where(x => x.Id == "ea07bd86-a507-4c68-9545-b848bfe74c90").Get())
            .Models.Should().ContainSingle();
        (await client.Table<Movie>().Where(x => x.Name!.Contains("Gun")).Get())
            .Models.Should().HaveCount(2);
        (await client.Table<Movie>()
                .Where(x => x.Name!.Contains("Gun") && x.CreatedAt <= new DateTimeOffset(new DateTime(2022, 8, 23)))
                .Get())
            .Models.Should().ContainSingle();
        (await client.Table<KitchenSink>().Where(x => x.StringValue != null).Get())
            .Models.Should().OnlyContain(m => m.StringValue != null);
        (await client.Table<Movie>().Where(x => x.Status == MovieStatus.OnDisplay).Get())
            .Models.Should().OnlyContain(m => m.Status == MovieStatus.OnDisplay);
    }

    [TestMethod]
    public async Task Where_ShouldSelectOnlyMatchingRows_GivenNullableValueComparisons()
    {
        var client = LocalStack.Client();
        var marker = $"nullable-value-{Guid.NewGuid()}";
        await client.Table<KitchenSink>().Insert(new List<KitchenSink>
        {
            new() { StringValue = marker, IntValue = null },
            new() { StringValue = marker, IntValue = 1 },
            new() { StringValue = marker, IntValue = 5 }
        });

        try
        {
            var matching = await client.Table<KitchenSink>()
                .Where(x => x.StringValue == marker && x.IntValue!.Value > 3).Get();
            matching.Models.Should().ContainSingle().Which.IntValue.Should().Be(5);

            var converted = await client.Table<KitchenSink>()
                .Where(x => x.StringValue == marker && (long) x.IntValue!.Value > 3L).Get();
            converted.Models.Should().ContainSingle().Which.Id.Should().Be(matching.Model!.Id);
        }
        finally
        {
            await client.Table<KitchenSink>().Where(x => x.StringValue == marker).Delete();
        }
    }

    [TestMethod]
    public async Task Not_ShouldExcludeMatchingRows_GivenTheExpressionOverload()
    {
        var client = LocalStack.Client();
        var filtered = await client.Table<User>().Not(x => x.Username!, Operator.Equals, "supabot").Get();
        var all = await client.Table<User>().Get();
        filtered.Models.Should().Equal(all.Models.Where(u => u.Username != "supabot").ToList());
    }

    [TestMethod]
    public async Task Where_ShouldSelectRowsMatchingAnyBranch_GivenThreeChainedOrs()
    {
        var client = LocalStack.Client();
        var response = await client.Table<User>()
            .Where(x => x.Username == "supabot" || x.Username == "kiwicopple" || x.Status == "OFFLINE").Get();
        response.Models.Should()
            .OnlyContain(m => m.Username == "supabot" || m.Username == "kiwicopple" || m.Status == "OFFLINE");
    }

    [TestMethod]
    public async Task Or_ShouldSelectRowsMatchingAnyBranch()
    {
        var client = LocalStack.Client();
        var filters = new List<IPostgrestQueryFilter>
        {
            new QueryFilter<User, List<string>>(x => x.Username!, Operator.In,
                new List<string> { "supabot", "kiwicopple" }),
            new QueryFilter<User, string>(x => x.Status!, Operator.Equals, "OFFLINE")
        };
        var response = await client.Table<User>().Or(filters).Get();
        response.Models.Should()
            .OnlyContain(m => m.Username == "supabot" || m.Username == "kiwicopple" || m.Status == "OFFLINE");
    }

    [TestMethod]
    public async Task Filter_ShouldSelectASingleRow_GivenTheExpressionOverload()
    {
        var response = await LocalStack.Client().Table<User>()
            .Filter(x => x.Username!, Operator.Equals, "supabot").Single();
        response.Should().NotBeNull();
        response!.Username.Should().Be("supabot");
    }

    [TestMethod]
    public async Task OnConflict_ShouldResolveOnTheNamedColumn_GivenTheExpressionOverload()
    {
        var client = LocalStack.Client();
        var existing = await client.Table<User>().Where(x => x.Username == "super-unique").Single();
        if (existing != null)
            await existing.Delete<User>();
        var user = new User { Username = "super-unique", Status = "ONLINE", FavoriteName = "supabase-2" };
        var inserted = await client.Table<User>().OnConflict(x => x.FavoriteName!).Insert(user);
        var resolved = await client.Table<User>()
            .OnConflict(x => x.FavoriteName!).Set(x => x.Status!, "OFFLINE").Upsert(inserted.Model!);
        resolved.Model.Should().NotBeNull();
        resolved.Model!.FavoriteName.Should().Be("supabase-2", "the upsert resolves onto the existing row");
    }

    [TestMethod]
    public async Task Columns_ShouldOnlyWriteTheProjectedColumns_GivenTheExpressionOverload()
    {
        var client = LocalStack.Client();
        var movie = (await client.Table<Movie>().Get()).Models.First();
        var originalDate = movie.CreatedAt;
        var newName = $"{movie.Name} (Changed, {Guid.NewGuid()})";
        movie.Name = newName;
        movie.CreatedAt = DateTime.UtcNow;
        var result = await client.Table<Movie>().Columns(x => new object[] { x.Name! }).Update(movie);
        result.Models.First().CreatedAt.Should().Be(originalDate);
        result.Models.First().Name.Should().Be(newName);
    }

    [TestMethod]
    public async Task Set_ShouldUpdateOnlyTheAssignedColumns()
    {
        var client = LocalStack.Client();
        var original = await client.Table<KitchenSink>()
            .Where(x => x.Id! == new Guid("f3ff356d-5803-43a7-b125-ba10cf10fdcd")).Single();
        original.Should().NotBeNull();
        var updated = await client.Table<KitchenSink>()
            .Set(x => x.BooleanValue!, !original!.BooleanValue!)
            .Set(x => x.IntValue!, original.IntValue! + 1)
            .Where(x => x.Id == original.Id)
            .Update(new QueryOptions { Returning = QueryOptions.ReturnType.Representation });
        var record = updated.Models[0];
        record.BooleanValue.Should().Be(!original.BooleanValue);
        record.IntValue.Should().Be(original.IntValue + 1);
    }

    [TestMethod]
    public async Task Delete_ShouldRemoveRowsMatchingThePredicate()
    {
        var client = LocalStack.Client();
        var newMovie = new Movie { Name = $"Pride and Prejudice {Guid.NewGuid()}", CreatedAt = DateTime.Now };
        await client.Table<Movie>().Insert(newMovie);
        await client.Table<Movie>().Where(x => x.Name == newMovie.Name).Delete();
        (await client.Table<Movie>().Where(x => x.Name == newMovie.Name).Single()).Should().BeNull();
    }
}
