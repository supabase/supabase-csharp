using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Filtering;

/// <summary>
///     How each <see cref="Operator" /> renders onto the PostgREST wire: <c>Table.PrepareFilter</c> turns a
///     <see cref="QueryFilter" /> into the <c>column =&gt; op.value</c> pair that becomes the query string.
///     These pin that rendering per operator family without touching the network.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class FilterSerializationTests
{
    private const string BaseUrl = "http://localhost:54321/rest/v1";

    private static KeyValuePair<string, string> Prepare(IPostgrestQueryFilter filter) =>
        ((Table<User>) new Client(BaseUrl).Table<User>()).PrepareFilter(filter);

    [TestMethod]
    public void PrepareFilter_ShouldRenderComparisonOperators()
    {
        var cases = new Dictionary<Operator, string>
        {
            { Operator.Equals, "eq.bar" },
            { Operator.GreaterThan, "gt.bar" },
            { Operator.GreaterThanOrEqual, "gte.bar" },
            { Operator.LessThan, "lt.bar" },
            { Operator.LessThanOrEqual, "lte.bar" },
            { Operator.NotEqual, "neq.bar" },
            { Operator.Is, "is.bar" }
        };
        using (new AssertionScope())
        {
            foreach (var (op, expected) in cases)
            {
                var result = Prepare(new QueryFilter("foo", op, "bar"));
                result.Key.Should().Be("foo");
                result.Value.Should().Be(expected);
            }
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldWrapPatternOperators_GivenLikeAndILike()
    {
        using (new AssertionScope())
        {
            Prepare(new QueryFilter("foo", Operator.Like, "%bar%")).Value.Should().Be("like.*bar*");
            Prepare(new QueryFilter("foo", Operator.ILike, "%bar%")).Value.Should().Be("ilike.*bar*");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldQuoteListItems_GivenInOperator()
    {
        var result = Prepare(new QueryFilter("foo", Operator.In, new List<object> { "bar", "buzz" }));
        result.Value.Should().Be("in.(\"bar\",\"buzz\")");
    }

    [TestMethod]
    public void PrepareFilter_ShouldRenderUnquotedBraces_GivenArrayContainmentOperators()
    {
        var list = new List<object> { "bar", "buzz" };
        using (new AssertionScope())
        {
            Prepare(new QueryFilter("foo", Operator.Contains, list)).Value.Should().Be("cs.{bar,buzz}");
            Prepare(new QueryFilter("foo", Operator.ContainedIn, list)).Value.Should().Be("cd.{bar,buzz}");
            Prepare(new QueryFilter("foo", Operator.Overlap, list)).Value.Should().Be("ov.{bar,buzz}");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldRenderJsonObject_GivenDictionaryCriteria()
    {
        var value = new Dictionary<string, object> { { "bar", 100 }, { "buzz", "zap" } };
        var expected = "{\"bar\":100,\"buzz\":\"zap\"}";
        using (new AssertionScope())
        {
            Prepare(new QueryFilter("foo", Operator.In, value)).Value.Should().Be($"in.{expected}");
            Prepare(new QueryFilter("foo", Operator.Contains, value)).Value.Should().Be($"cs.{expected}");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldEmbedLanguageConfig_GivenFullTextSearchOperators()
    {
        using (new AssertionScope())
        {
            var config = new FullTextSearchConfig("bar", "english");
            Prepare(new QueryFilter("foo", Operator.FTS, config)).Value.Should().Be("fts(english).bar");
            Prepare(new QueryFilter("foo", Operator.PHFTS, config)).Value.Should().Be("phfts(english).bar");
            Prepare(new QueryFilter("foo", Operator.PLFTS, config)).Value.Should().Be("plfts(english).bar");
            Prepare(new QueryFilter("foo", Operator.WFTS, config)).Value.Should().Be("wfts(english).bar");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldRenderInclusiveBounds_GivenRangeOperators()
    {
        var range = new IntRange(2, 3);
        using (new AssertionScope())
        {
            Prepare(new QueryFilter("foo", Operator.StrictlyLeft, range)).Value.Should().Be("sl.[2,3]");
            Prepare(new QueryFilter("foo", Operator.StrictlyRight, range)).Value.Should().Be("sr.[2,3]");
            Prepare(new QueryFilter("foo", Operator.NotRightOf, range)).Value.Should().Be("nxr.[2,3]");
            Prepare(new QueryFilter("foo", Operator.NotLeftOf, range)).Value.Should().Be("nxl.[2,3]");
            Prepare(new QueryFilter("foo", Operator.Adjacent, range)).Value.Should().Be("adj.[2,3]");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldPrefixWithNot_GivenNegatedFilter()
    {
        var negated = new QueryFilter(Operator.Not, new QueryFilter("foo", Operator.Equals, "bar"));
        var result = Prepare(negated);
        using (new AssertionScope())
        {
            result.Key.Should().Be("foo");
            result.Value.Should().Be("not.eq.bar");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldPrefixKeyWithNot_GivenNegatedLogicalGroup()
    {
        var group = new QueryFilter(Operator.And, new List<IPostgrestQueryFilter>
        {
            new QueryFilter("a", Operator.GreaterThanOrEqual, "0"),
            new QueryFilter("a", Operator.LessThanOrEqual, "100")
        });
        var result = Prepare(new QueryFilter(Operator.Not, group));
        using (new AssertionScope())
        {
            result.Key.Should().Be("not.and");
            result.Value.Should().Be("(a.gte.0,a.lte.100)");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldJoinChildren_GivenAndOrGroups()
    {
        var children = new List<IPostgrestQueryFilter>
        {
            new QueryFilter("a", Operator.GreaterThanOrEqual, "0"),
            new QueryFilter("a", Operator.LessThanOrEqual, "100")
        };
        using (new AssertionScope())
        {
            var and = Prepare(new QueryFilter(Operator.And, children));
            (and.Key + "=" + and.Value).Should().Be("and=(a.gte.0,a.lte.100)");
            var or = Prepare(new QueryFilter(Operator.Or, children));
            (or.Key + "=" + or.Value).Should().Be("or=(a.gte.0,a.lte.100)");
        }
    }

    [TestMethod]
    public void PrepareFilter_ShouldDropTheDot_GivenNestedGroups()
    {
        var inner = new QueryFilter(Operator.And, new List<IPostgrestQueryFilter>
        {
            new QueryFilter("a", Operator.GreaterThanOrEqual, "0"),
            new QueryFilter("a", Operator.LessThanOrEqual, "100")
        });
        var middle = new QueryFilter(Operator.Or, new List<IPostgrestQueryFilter>
        {
            inner,
            new QueryFilter("b", Operator.Equals, "bar")
        });
        var outer = new QueryFilter(Operator.And, new List<IPostgrestQueryFilter>
        {
            middle,
            new QueryFilter("c", Operator.Equals, "buzz")
        });
        var result = Prepare(outer);
        (result.Key + "=" + result.Value).Should()
            .Be("and=(or(and(a.gte.0,a.lte.100),b.eq.bar),c.eq.buzz)");
    }

    [TestMethod]
    public void PrepareFilter_ShouldDropTheDot_GivenNegatedGroupInsideGroup()
    {
        var negated = new QueryFilter(Operator.Not, new QueryFilter(Operator.And, new List<IPostgrestQueryFilter>
        {
            new QueryFilter("a", Operator.GreaterThanOrEqual, "0"),
            new QueryFilter("a", Operator.LessThanOrEqual, "100")
        }));
        var result = Prepare(new QueryFilter(Operator.Or, new List<IPostgrestQueryFilter>
        {
            negated,
            new QueryFilter("b", Operator.Equals, "bar")
        }));
        (result.Key + "=" + result.Value).Should().Be("or=(not.and(a.gte.0,a.lte.100),b.eq.bar)");
    }

    [TestMethod]
    public void PrepareFilter_ShouldKeepTheDot_GivenValueOperatorsInsideGroup()
    {
        var result = Prepare(new QueryFilter(Operator.Or, new List<IPostgrestQueryFilter>
        {
            new QueryFilter("foo", Operator.In, new List<object> { "bar", "buzz" }),
            new QueryFilter("foo", Operator.Contains, new List<object> { "bar", "buzz" }),
            new QueryFilter("foo", Operator.FTS, new FullTextSearchConfig("bar", "english"))
        }));
        (result.Key + "=" + result.Value).Should()
            .Be("or=(foo.in.(\"bar\",\"buzz\"),foo.cs.{bar,buzz},foo.fts(english).bar)");
    }
}
