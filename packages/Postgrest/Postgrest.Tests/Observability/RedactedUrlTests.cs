using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;

namespace Postgrest.Tests.Observability;

/// <summary>
///     Covers <see cref="RedactedUrl" />, the form of a request URL that is safe to record: query keys are kept,
///     values are replaced by REDACTED except for the keys that only shape the result.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RedactedUrlTests
{
    private const string Base = "https://project.supabase.co/rest/v1/users";

    [TestMethod]
    public void Of_ShouldReturnTheBareUrl_GivenNoQuery() =>
        RedactedUrl.Of(new Uri(Base)).Should().Be(Base);

    [TestMethod]
    public void Of_ShouldRedactFilterValuesAndKeepTheirKeys() =>
        RedactedUrl.Of(new Uri($"{Base}?email=eq.john@doe.com")).Should().Be($"{Base}?email=REDACTED");

    [TestMethod]
    public void Of_ShouldKeepTheValuesOfSelectOrderLimitAndOffset() =>
        RedactedUrl.Of(new Uri($"{Base}?select=id,email&email=eq.john@doe.com&order=created_at.desc&limit=10&offset=5"))
            .Should().Be($"{Base}?select=id,email&email=REDACTED&order=created_at.desc&limit=10&offset=5");

    [TestMethod]
    public void Of_ShouldRedactEveryOccurrenceOfARepeatedKey() =>
        RedactedUrl.Of(new Uri($"{Base}?age=gte.18&age=lt.65")).Should().Be($"{Base}?age=REDACTED&age=REDACTED");

    [TestMethod]
    public void Of_ShouldRedactAKeyThatHasNoValue() =>
        RedactedUrl.Of(new Uri($"{Base}?flag")).Should().Be($"{Base}?flag=REDACTED");

    [TestMethod]
    public void Of_ShouldRedactAValueWithNoKey() =>
        RedactedUrl.Of(new Uri($"{Base}?=secret")).Should().Be($"{Base}?=REDACTED");

    [TestMethod]
    public void Of_ShouldKeepASafeKeyThatHasNoValue() =>
        RedactedUrl.Of(new Uri($"{Base}?select")).Should().Be($"{Base}?select");

    [TestMethod]
    public void Of_ShouldDropUserInfoAndFragment() =>
        RedactedUrl.Of(new Uri("https://user:password@project.supabase.co/rest/v1/users?limit=1#token")).Should().Be($"{Base}?limit=1");
}
