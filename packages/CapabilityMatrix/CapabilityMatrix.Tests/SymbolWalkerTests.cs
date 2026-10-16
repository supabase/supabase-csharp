using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

[TestClass]
public class SymbolWalkerTests
{
    private static IReadOnlyList<DeclaredSymbol> Extract(string source) =>
        SymbolWalker.Extract([new SymbolWalker.SourceFile("Sample.cs", source)]);

    private static DeclaredSymbol Named(IReadOnlyList<DeclaredSymbol> symbols, string name) =>
        symbols.Single(s => s.QualifiedName == name);

    [TestMethod]
    public void StripsNamespaceAndQualifiesByContainingType()
    {
        var symbols = Extract(
            """
            namespace Foo.Bar
            {
                public class Widget
                {
                    public void Go() { }
                }
            }
            """);

        symbols.Select(s => s.QualifiedName).Should().Contain(["Widget", "Widget.Go"]);
        symbols.Select(s => s.QualifiedName).Should().NotContain(n => n.StartsWith("Foo"));
        Named(symbols, "Widget").Kind.Should().Be("class");
        Named(symbols, "Widget.Go").Kind.Should().Be("method");
    }

    [TestMethod]
    public void QualifiesNestedTypesEnumMembersAndFields()
    {
        var symbols = Extract(
            """
            namespace N
            {
                public static class Constants
                {
                    public enum Operator { Equals, NotEqual }
                }
                public class Options
                {
                    public string Headers = "";
                }
            }
            """);

        symbols.Select(s => s.QualifiedName).Should().Contain(
            ["Constants", "Constants.Operator", "Constants.Operator.Equals", "Constants.Operator.NotEqual", "Options.Headers"]);
        Named(symbols, "Constants.Operator.Equals").Kind.Should().Be("property");
        Named(symbols, "Options.Headers").Kind.Should().Be("property");
    }

    [TestMethod]
    public void MarksAccessibilityAcrossTheContainingChain()
    {
        var symbols = Extract(
            """
            namespace N
            {
                public class Pub
                {
                    public void A() { }
                    internal void B() { }
                    private void C() { }
                    public static class Inner { public void D() { } }
                }
                internal class Hidden { public void E() { } }
                public interface IFace { void F(); }
            }
            """);

        Named(symbols, "Pub.A").IsPublic.Should().BeTrue();
        Named(symbols, "Pub.B").IsPublic.Should().BeFalse();
        Named(symbols, "Pub.C").IsPublic.Should().BeFalse();
        Named(symbols, "Pub.Inner.D").IsPublic.Should().BeTrue();
        Named(symbols, "Hidden").IsPublic.Should().BeFalse();
        Named(symbols, "Hidden.E").IsPublic.Should().BeFalse("the containing type is not public");
        Named(symbols, "IFace.F").IsPublic.Should().BeTrue("interface members are public by default");
    }

    [TestMethod]
    public void ReadsBothCapabilityAttributesWithNotes()
    {
        var symbols = Extract(
            """
            using Supabase.Core.Attributes;
            namespace N
            {
                public class C
                {
                    [ImplementedCapability("a.b.c")]
                    public void Plain() { }

                    [ImplementedCapability("a.b.d", "with caveat")]
                    public void Caveat() { }

                    [PartialCapability("a.b.e", "missing X")]
                    public void Partial() { }
                }
            }
            """);

        Named(symbols, "C.Plain").Annotations.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "a.b.c", Status = CapabilityStatus.Implemented, Note = (string?) null });
        Named(symbols, "C.Caveat").Annotations.Single().Note.Should().Be("with caveat");
        var partial = Named(symbols, "C.Partial").Annotations.Single();
        partial.Status.Should().Be(CapabilityStatus.PartiallyImplemented);
        partial.Note.Should().Be("missing X");
    }

    [TestMethod]
    public void AcceptsAttributeSuffixQualifiedNameAndNamedNoteArgument()
    {
        var symbols = Extract(
            """
            namespace N
            {
                public class C
                {
                    [Supabase.Core.Attributes.ImplementedCapabilityAttribute("a.b.c", note: "n")]
                    public void M() { }

                    [System.Obsolete]
                    public void Ignored() { }
                }
            }
            """);

        Named(symbols, "C.M").Annotations.Single().Should()
            .BeEquivalentTo(new { Key = "a.b.c", Note = "n" });
        Named(symbols, "C.Ignored").Annotations.Should().BeEmpty();
    }

    [TestMethod]
    public void AllowsMultipleCapabilitiesOnOneMember()
    {
        var symbols = Extract(
            """
            using Supabase.Core.Attributes;
            namespace N
            {
                public class C
                {
                    [ImplementedCapability("a.one")]
                    [ImplementedCapability("a.two")]
                    public void M() { }
                }
            }
            """);

        Named(symbols, "C.M").Annotations.Select(a => a.Key).Should().BeEquivalentTo(["a.one", "a.two"]);
    }

    [TestMethod]
    public void SkipsAttributesWhoseKeyIsNotAStringLiteral()
    {
        var symbols = Extract(
            """
            using Supabase.Core.Attributes;
            namespace N
            {
                public class C
                {
                    public const string K = "a.b.c";
                    [ImplementedCapability(K)]
                    public void M() { }
                }
            }
            """);

        Named(symbols, "C.M").Annotations.Should().BeEmpty("a non-literal key cannot be resolved from syntax");
    }

    [TestMethod]
    public void OrdersDeterministicallyAndReportsOneBasedLines()
    {
        var symbols = Extract(
            """
            namespace N
            {
                public class B { public void Z() { } }
                public class A { public void Y() { } }
            }
            """);

        symbols.Select(s => s.QualifiedName).Should().BeInAscendingOrder(StringComparer.Ordinal);
        Named(symbols, "A").Line.Should().BeGreaterThan(0);
    }
}
