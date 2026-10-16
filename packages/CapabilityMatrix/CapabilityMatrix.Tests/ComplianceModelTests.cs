using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

[TestClass]
public class ComplianceModelTests
{
    private static DeclaredSymbol Sym(string name, bool isPublic, params CapabilityAnnotation[] annotations) =>
        new(name, "method", "F.cs", 1, isPublic, annotations);

    private static CapabilityAnnotation Impl(string key, string? note = null) => new(key, CapabilityStatus.Implemented, note);

    private static CapabilityAnnotation Part(string key, string note) => new(key, CapabilityStatus.PartiallyImplemented, note);

    private static ComplianceEntry Entry(ComplianceResult result, string key) => result.Entries.Single(e => e.Key == key);

    [TestMethod]
    public void AggregatesSymbolsPerFeatureSortedAndDeduplicated()
    {
        var result = ComplianceModel.Build(
        [
            Sym("Client.SignInWithPassword", true, Impl("auth.sign_in")),
            Sym("Client.SignIn", true, Impl("auth.sign_in")),
            Sym("Client.SignIn", true, Impl("auth.sign_in")),
        ]);

        result.Errors.Should().BeEmpty();
        Entry(result, "auth.sign_in").Symbols.Should().Equal("Client.SignIn", "Client.SignInWithPassword");
    }

    [TestMethod]
    public void CarriesStatusAndNoteForImplementedAndPartial()
    {
        var result = ComplianceModel.Build(
        [
            Sym("Client.Invoke", true, Impl("fn.invoke", "three shapes")),
            Sym("Client.SignUp", true, Part("auth.sign_up", "no session adopt")),
        ]);

        Entry(result, "fn.invoke").Should().BeEquivalentTo(new
        {
            Status = CapabilityStatus.Implemented,
            Note = "three shapes",
            NoPublicSymbols = false,
        });
        Entry(result, "auth.sign_up").Status.Should().Be(CapabilityStatus.PartiallyImplemented);
    }

    [TestMethod]
    public void ReportsConflictingStatusesForOneFeature()
    {
        var result = ComplianceModel.Build(
        [
            Sym("A", true, Impl("x.y")),
            Sym("B", true, Part("x.y", "partial")),
        ]);

        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("conflicting statuses");
    }

    [TestMethod]
    public void ReportsConflictingNotesForOneFeature()
    {
        var result = ComplianceModel.Build(
        [
            Sym("A", true, Part("x.y", "note one")),
            Sym("B", true, Part("x.y", "note two")),
        ]);

        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("differing notes");
    }

    [TestMethod]
    public void TreatsANonPublicOnlyFeatureAsNoPublicSymbols()
    {
        var result = ComplianceModel.Build(
        [
            Sym("Client.GetAuthHeaders", false, Impl("client.sync", "injected via callback")),
        ]);

        result.Errors.Should().BeEmpty();
        var entry = Entry(result, "client.sync");
        entry.NoPublicSymbols.Should().BeTrue();
        entry.Symbols.Should().BeEmpty();
        entry.Note.Should().Be("injected via callback");
    }

    [TestMethod]
    public void RequiresANoteWhenThereIsNoPublicSymbol()
    {
        var result = ComplianceModel.Build(
        [
            Sym("Internal.Thing", false, Impl("client.sync")),
        ]);

        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("needs a note");
    }

    [TestMethod]
    public void KeepsOnlyPublicSymbolsWhenAFeatureHasBoth()
    {
        var result = ComplianceModel.Build(
        [
            Sym("Public.M", true, Impl("x.y")),
            Sym("Internal.M", false, Impl("x.y")),
        ]);

        var entry = Entry(result, "x.y");
        entry.NoPublicSymbols.Should().BeFalse();
        entry.Symbols.Should().Equal("Public.M");
    }

    [TestMethod]
    public void IgnoresSymbolsWithoutAnnotations()
    {
        var result = ComplianceModel.Build([Sym("Plain.Method", true)]);
        result.Entries.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }
}

[TestClass]
public class KeyValidatorTests
{
    [TestMethod]
    public void FlagsUnknownKeysOnly()
    {
        var entries = new List<ComplianceEntry>
        {
            new("known.feature", CapabilityStatus.Implemented, null, ["A"]),
            new("typo.feature", CapabilityStatus.Implemented, null, ["B"]),
        };
        var known = new HashSet<string>(StringComparer.Ordinal) { "known.feature" };

        var errors = KeyValidator.ValidateKeys(entries, known);

        errors.Should().ContainSingle().Which.Message.Should().Contain("typo.feature");
    }
}
