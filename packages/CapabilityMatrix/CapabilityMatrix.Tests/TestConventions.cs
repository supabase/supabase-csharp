using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Supabase.CapabilityMatrix.Tests;

/// <summary>
/// Mechanized guardrails for the suite itself: these fail the build when a test class or method drifts
/// from the documented conventions, so the rules are enforced deterministically instead of by review.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class TestConventions
{
    private static readonly Regex NamePattern =
        new("^[A-Za-z][A-Za-z0-9]*_Should[A-Za-z0-9]+(_Given[A-Za-z0-9]+)?$", RegexOptions.Compiled);

    private static readonly Type[] TestClasses = typeof(TestConventions).Assembly.GetTypes()
        .Where(type => type.GetCustomAttribute<TestClassAttribute>() != null)
        .ToArray();

    [TestMethod]
    public void TestClass_ShouldDeclareExactlyOneTestCategory()
    {
        using var scope = new AssertionScope();
        foreach (var type in TestClasses)
            type.GetCustomAttributes<TestCategoryBaseAttribute>().Should().ContainSingle(
                $"{type.Name} must carry exactly one [TestCategory] tier (Unit/Contract/E2E)");
    }

    [TestMethod]
    public void TestMethod_ShouldFollowTheNamingConvention()
    {
        using var scope = new AssertionScope();
        var testMethods = TestClasses
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttribute<TestMethodAttribute>() != null);
        foreach (var method in testMethods)
            method.Name.Should().MatchRegex(NamePattern,
                $"{method.DeclaringType!.Name}.{method.Name} must read Sut_ShouldConsequence[_GivenScenario]");
    }
}
