using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;

namespace Postgrest.Tests.Linq;

/// <summary>
///     How a LINQ <c>Where</c> predicate is translated into PostgREST query syntax, asserted on the generated
///     URL so no network is involved. Covers negation, null-checks, logical grouping (nested groups render
///     dotless, supabase-csharp#336), boolean members, and the <c>Contains</c> disambiguation between a
///     captured collection (<c>in</c>) and a column (<c>cs</c>/<c>like</c>); unsupported predicates are
///     rejected with a descriptive <see cref="ArgumentException" /> (supabase-csharp#192).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class WhereClauseTests
{
    private const string BaseUrl = "http://localhost:54321/rest/v1";
    private readonly Client client = new(BaseUrl);

    [TestMethod]
    public void Where_ShouldApplyNoFilter_GivenAnAbsentDelegateNullCheck()
    {
        var requestModel = new UserRequestModel();
        this.client.Table<User>().Where(x => requestModel.FilterPredicate == null || requestModel.FilterPredicate(x))
            .GenerateUrl().Should().Be($"{BaseUrl}/users");
    }

    [TestMethod]
    public void Where_ShouldThrowDescriptive_GivenAPresentDelegateItCannotTranslate()
    {
        var requestModel = new UserRequestModel { FilterPredicate = u => u.Username == "supabot" };
        var act = () => this.client.Table<User>()
            .Where(x => requestModel.FilterPredicate == null || requestModel.FilterPredicate(x));
        act.Should().Throw<ArgumentException>().WithMessage("*Unable to translate expression*");
    }

    [TestMethod]
    public void Where_ShouldThrowDescriptive_GivenAnAlwaysFalsePredicate()
    {
        var requestModel = new UserRequestModel();
        var act = () => this.client.Table<User>()
            .Where(x => requestModel.FilterPredicate != null && requestModel.FilterPredicate(x));
        act.Should().Throw<ArgumentException>().WithMessage("*always evaluates to false*");
    }

    [TestMethod]
    public void Where_ShouldTranslateToIsNull_GivenANullCheckInsideAnOrPredicate()
    {
        this.client.Table<User>().Where(x => x.Catchphrase == null || x.Catchphrase == "fat cat")
            .GenerateUrl().Should().Be($"{BaseUrl}/users?or=(catchphrase.is.null%2ccatchphrase.eq.fat+cat)");
    }

    [TestMethod]
    public void Where_ShouldNegateEqualityIntoNotEq()
    {
        this.client.Table<User>().Where(x => !(x.Username == "supabot"))
            .GenerateUrl().Should().Be($"{BaseUrl}/users?username=not.eq.supabot");
    }

    [TestMethod]
    public void Where_ShouldNegateNullCheckIntoNotIsNull()
    {
        this.client.Table<User>().Where(x => !(x.Catchphrase == null))
            .GenerateUrl().Should().Be($"{BaseUrl}/users?catchphrase=not.is.null");
    }

    [TestMethod]
    public void Where_ShouldNegateGroupedPredicateIntoNotWrappedLogicalFilter()
    {
        this.client.Table<User>().Where(x => !(x.Catchphrase == "fat cat" || x.Username == "supabot"))
            .GenerateUrl().Should().Be($"{BaseUrl}/users?not.or=(catchphrase.eq.fat+cat%2cusername.eq.supabot)");
    }

    [TestMethod]
    public void Where_ShouldDropTheDot_GivenThreeChainedOrs()
    {
        const string search = "Ran";
        this.client.Table<User>()
            .Where(x => x.Catchphrase!.Contains(search) || x.FavoriteName!.Contains(search) ||
                        x.Username!.Contains(search))
            .GenerateUrl().Should()
            .Be($"{BaseUrl}/users?or=(or(catchphrase.like.*Ran*%2cfavorite_name.like.*Ran*)%2cusername.like.*Ran*)");
    }

    [TestMethod]
    public void Where_ShouldDropTheDot_GivenAnAndGroupInsideAnOrPredicate()
    {
        this.client.Table<User>()
            .Where(x => x.Username == "supabot" || (x.Catchphrase == "fat cat" && x.Status == "ONLINE"))
            .GenerateUrl().Should()
            .Be($"{BaseUrl}/users?or=(username.eq.supabot%2cand(catchphrase.eq.fat+cat%2cstatus.eq.ONLINE))");
    }

    [TestMethod]
    public void Where_ShouldDropTheDot_GivenANegatedAndGroupInsideAnOrPredicate()
    {
        this.client.Table<User>()
            .Where(x => !(x.Catchphrase == "fat cat" && x.Username == "supabot") || x.Status == "ONLINE")
            .GenerateUrl().Should()
            .Be($"{BaseUrl}/users?or=(not.and(catchphrase.eq.fat+cat%2cusername.eq.supabot)%2cstatus.eq.ONLINE)");
    }

    [TestMethod]
    public void Where_ShouldKeepTheDot_GivenNegatedColumnFiltersInsideAGroup()
    {
        this.client.Table<User>()
            .Where(x => x.Catchphrase != null && !(x.Username == "supabot"))
            .GenerateUrl().Should()
            .Be($"{BaseUrl}/users?and=(catchphrase.not.is.null%2cusername.not.eq.supabot)");
    }

    [TestMethod]
    public void Where_ShouldNegateStringContainsIntoNotLike()
    {
        this.client.Table<User>().Where(x => !x.Username!.Contains("supa"))
            .GenerateUrl().Should().Be($"{BaseUrl}/users?username=not.like.*supa*");
    }

    [TestMethod]
    public void Where_ShouldTranslateCapturedListContainsColumnIntoIn()
    {
        var values = new List<string> { "a", "b" };
        this.client.Table<KitchenSink>().Where(x => values.Contains(x.StringValue!))
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?string_value=in.(\"a\"%2c\"b\")");
    }

    [TestMethod]
    public void Where_ShouldTranslateCapturedArrayContainsColumnIntoIn()
    {
        var values = new[] { 1, 2 };
        this.client.Table<KitchenSink>().Where(x => values.Contains(x.IntValue!.Value))
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?int_value=in.(\"1\"%2c\"2\")");
    }

    [TestMethod]
    public void Where_ShouldTranslateColumnListContainsConstantIntoContains()
    {
        this.client.Table<KitchenSink>().Where(x => x.ListOfStrings!.Contains("set"))
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?list_of_strings=cs.{{set}}");
    }

    [TestMethod]
    public void Where_ShouldTranslateColumnStringContainsConstantIntoLike()
    {
        this.client.Table<KitchenSink>().Where(x => x.StringValue!.Contains("foo"))
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?string_value=like.*foo*");
    }

    [TestMethod]
    public void Where_ShouldTranslateBareBooleanMemberIntoEqTrue()
    {
        this.client.Table<KitchenSink>().Where(x => x.BooleanValue)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?bool_value=eq.True");
    }

    [TestMethod]
    public void Where_ShouldTranslateNegatedBooleanMemberIntoNotEqTrue()
    {
        this.client.Table<KitchenSink>().Where(x => !x.BooleanValue)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?bool_value=not.eq.True");
    }

    [TestMethod]
    public void Where_ShouldUseColumnName_GivenBareNullableBooleanValueMember()
    {
        this.client.Table<NullableFlag>().Where(x => x.IsActive!.Value)
            .GenerateUrl().Should().Be($"{BaseUrl}/nullable_flag?is_active=eq.True");
    }

    [TestMethod]
    public void Where_ShouldUseColumnName_GivenNegatedNullableBooleanValueMember()
    {
        this.client.Table<NullableFlag>().Where(x => !x.IsActive!.Value)
            .GenerateUrl().Should().Be($"{BaseUrl}/nullable_flag?is_active=not.eq.True");
    }

    [TestMethod]
    public void Where_ShouldNestBooleanMember_GivenAnAndPredicate()
    {
        this.client.Table<KitchenSink>().Where(x => x.BooleanValue && x.IntValue > 3)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?and=(bool_value.eq.True%2cint_value.gt.3)");
    }

    [TestMethod]
    public void Where_ShouldUseColumnName_GivenNullableValueComparison()
    {
        this.client.Table<KitchenSink>().Where(x => x.IntValue!.Value > 3)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?int_value=gt.3");
    }

    [TestMethod]
    public void Where_ShouldUseColumnName_GivenConvertedNullableValueComparison()
    {
        this.client.Table<KitchenSink>().Where(x => (long) x.IntValue!.Value > 3L)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?int_value=gt.3");
    }

    [TestMethod]
    public void Where_ShouldUsePrimaryKeyName_GivenNullableValueComparison()
    {
        var identifier = new Guid("f3ff356d-5803-43a7-b125-ba10cf10fdcd");
        this.client.Table<KitchenSink>().Where(x => x.Id!.Value == identifier)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?id=eq.{identifier}");
    }

    [TestMethod]
    public void Where_ShouldEvaluateCapturedNullableValue_GivenColumnComparison()
    {
        int? threshold = 3;
        this.client.Table<KitchenSink>().Where(x => x.IntValue > threshold.Value)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?int_value=gt.3");
    }

    [TestMethod]
    public void Where_ShouldThrowDescriptive_GivenTwoColumnsCompared()
    {
        var act = () => this.client.Table<KitchenSink>().Where(x => x.DateTimeValue < x.DateTimeValue1);
        act.Should().Throw<ArgumentException>().WithMessage("*cannot compare two model columns*");
    }

    [TestMethod]
    public void Where_ShouldEvaluateANegatedCapturedBool()
    {
        var pending = true;
        this.client.Table<Todo>().Where(x => x.Done == !pending)
            .GenerateUrl().Should().Be($"{BaseUrl}/todos?done=eq.False");
    }

    [TestMethod]
    public void Where_ShouldEvaluateAUnaryMinusOnACapturedInt()
    {
        var threshold = 100;
        this.client.Table<Todo>().Where(x => x.UserId < -threshold)
            .GenerateUrl().Should().Be($"{BaseUrl}/todos?user_id=lt.-100");
    }

    [TestMethod]
    public void Where_ShouldUseTheEnumName_GivenAnEnumColumn()
    {
        this.client.Table<Movie>().Where(x => x.Status == MovieStatus.OnDisplay)
            .GenerateUrl().Should().StartWith($"{BaseUrl}/movie?status=eq.OnDisplay&select=");
    }

    [TestMethod]
    public void Where_ShouldKeepAnExplicitCast_GivenADoubleColumn()
    {
        var threshold = 100.9;
        this.client.Table<KitchenSink>().Where(x => x.DoubleValue < (int) threshold)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?double_value=lt.100");
    }

    [TestMethod]
    public void Where_ShouldKeepAnEnumToIntCast_GivenAnIntColumn()
    {
        var status = MovieStatus.OffDisplay;
        this.client.Table<Todo>().Where(x => x.UserId == (int) status)
            .GenerateUrl().Should().Be($"{BaseUrl}/todos?user_id=eq.1");
    }

    [TestMethod]
    public void Where_ShouldKeepTheTest_GivenATernaryWithAFalseThenBranch()
    {
        this.client.Table<KitchenSink>().Where(x => x.BooleanValue ? false : x.IntValue > 3)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?and=(bool_value.not.eq.True%2cint_value.gt.3)");
    }

    [TestMethod]
    public void Where_ShouldKeepTheTest_GivenATernaryWithAFalseElseBranch()
    {
        this.client.Table<KitchenSink>().Where(x => x.BooleanValue ? x.IntValue > 3 : false)
            .GenerateUrl().Should().Be($"{BaseUrl}/kitchen_sink?and=(bool_value.eq.True%2cint_value.gt.3)");
    }

    [TestMethod]
    public void Where_ShouldTranslateTernaryIntoOrOfAnds_GivenTwoColumnBranches()
    {
        this.client.Table<KitchenSink>().Where(x => x.BooleanValue ? x.IntValue > 3 : x.StringValue == "foo")
            .GenerateUrl().Should()
            .Be($"{BaseUrl}/kitchen_sink?or=(and(bool_value.eq.True%2cint_value.gt.3)%2cand(bool_value.not.eq.True%2cstring_value.eq.foo))");
    }

    private class UserRequestModel
    {
        public Func<User, bool>? FilterPredicate { get; set; }
    }
}
