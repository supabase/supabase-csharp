using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;

namespace Postgrest.Tests.Linq;

/// <summary>
///     <c>Set</c> translates a <c>column =&gt; value</c> assignment for an update. The key must resolve to a
///     model column and the value must match its type; anything else is rejected synchronously with an
///     <see cref="ArgumentException" />, before any request is built.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SetClauseTests
{
    private const string BaseUrl = "http://localhost:54321/rest/v1";
    private readonly Client client = new(BaseUrl);

    [TestMethod]
    public void Set_ShouldAcceptNull_GivenAReferenceTypeColumn()
    {
        var act = () => this.client.Table<KitchenSink>().Set(model => model.ListOfStrings!, null);
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Set_ShouldAcceptNull_GivenAJsonObjectColumn()
    {
        var act = () => this.client.Table<UserWithJsonData>().Set(user => user.Data!, null);
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Set_ShouldAcceptNull_GivenAStringColumn()
    {
        var act = () => this.client.Table<KitchenSink>().Set(model => model.StringValue!, null);
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Set_ShouldAcceptNull_GivenANullableValueTypeColumn()
    {
        var act = () => this.client.Table<KitchenSink>().Set(model => model.IntValue!, null);
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Set_ShouldThrow_GivenNullForANonNullableValueTypeColumn()
    {
        var act = () => this.client.Table<KitchenSink>().Set(model => model.BooleanValue, null);
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Set_ShouldThrow_GivenAValueOfTheWrongType()
    {
        var act = () => this.client.Table<Movie>().Set(x => x.Name!, DateTime.Now);
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Set_ShouldThrow_GivenAKeyThatIsNotAColumn()
    {
        var act = () => this.client.Table<Movie>().Set(x => DateTime.Now, "value");
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Set_ShouldThrow_GivenAKeyValuePairWithAMismatchedValueType()
    {
        var act = () => this.client.Table<Movie>().Set(x => new KeyValuePair<object, object?>(x.Name!, DateTime.Now));
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Set_ShouldThrow_GivenAKeyValuePairWhoseKeyIsNotAColumn()
    {
        var act = () => this.client.Table<Movie>().Set(x => new KeyValuePair<object, object?>(DateTime.Now, "value"));
        act.Should().Throw<ArgumentException>();
    }
}
