using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

internal sealed class InMemoryWorkspace : IWorkspace
{
    public List<SymbolWalker.SourceFile> Sources { get; } = [];
    public List<string> SpecDocuments { get; } = [];
    public string ComplianceText { get; set; } = "sdk: csharp\nfeatures:\n";
    public string? Written { get; private set; }

    public IReadOnlyList<SymbolWalker.SourceFile> ReadSources() => this.Sources;

    public IReadOnlyList<string> ReadSpecDocuments() => this.SpecDocuments;

    public string ReadComplianceText() => this.ComplianceText;

    public void WriteComplianceText(string content) => this.Written = content;
}

[TestClass]
public class CommandRunnerTests
{
    private const string Spec =
        """
        area: auth
        title: Authentication
        groups:
          - id: sign_in
            title: Sign-in
        features:
          - id: auth.sign_in.sign_up
            group: sign_in
        """;

    private static InMemoryWorkspace WorkspaceWith(string source, string? compliance = null)
    {
        var workspace = new InMemoryWorkspace();
        workspace.Sources.Add(new SymbolWalker.SourceFile("packages/Gotrue/Gotrue/Client.cs", source));
        workspace.SpecDocuments.Add(Spec);
        if (compliance is not null)
            workspace.ComplianceText = compliance;
        return workspace;
    }

    private const string GoodSource =
        """
        using Supabase.Core.Attributes;
        namespace Supabase.Gotrue
        {
            public class Client
            {
                [ImplementedCapability("auth.sign_in.sign_up")]
                public void SignUp() { }
            }
        }
        """;

    private static (int Code, string Output) Run(Func<CommandRunner, int> act, IWorkspace workspace)
    {
        var writer = new StringWriter();
        var code = act(new CommandRunner(workspace, writer));
        return (code, writer.ToString());
    }

    [TestMethod]
    public void GenerateWritesTheFileOnACleanTree()
    {
        var workspace = WorkspaceWith(GoodSource);
        var (code, output) = Run(r => r.Generate(), workspace);

        code.Should().Be(CommandRunner.Success);
        output.Should().Contain("✅");
        workspace.Written.Should().Contain("auth.sign_in.sign_up:").And.Contain("- Client.SignUp");
    }

    [TestMethod]
    public void GenerateFailsAndWritesNothingOnAnUnknownKey()
    {
        var source = GoodSource.Replace("auth.sign_in.sign_up", "auth.sign_in.typo");
        var workspace = WorkspaceWith(source);
        var (code, output) = Run(r => r.Generate(), workspace);

        code.Should().Be(CommandRunner.Failure);
        output.Should().Contain("Cannot generate").And.Contain("auth.sign_in.typo");
        workspace.Written.Should().BeNull();
    }

    [TestMethod]
    public void ValidatePassesOnACleanTreeAndFailsOnConflict()
    {
        Run(r => r.Validate(), WorkspaceWith(GoodSource)).Code.Should().Be(CommandRunner.Success);

        var conflicting = GoodSource.Replace(
            "[ImplementedCapability(\"auth.sign_in.sign_up\")]",
            "[ImplementedCapability(\"auth.sign_in.sign_up\")]\n        [PartialCapability(\"auth.sign_in.sign_up\", \"partial\")]");
        var (code, output) = Run(r => r.Validate(), WorkspaceWith(conflicting));
        code.Should().Be(CommandRunner.Failure);
        output.Should().Contain("conflicting statuses");
    }

    [TestMethod]
    public void CheckPassesWhenCommittedMatchesAndFailsOtherwise()
    {
        var workspace = WorkspaceWith(GoodSource);
        workspace.ComplianceText = YamlEmitter.Emit(
            ComplianceModel.Build(SymbolWalker.Extract(workspace.Sources)).Entries,
            SpecModel.Parse(workspace.SpecDocuments));
        Run(r => r.Check(), workspace).Code.Should().Be(CommandRunner.Success);

        var stale = WorkspaceWith(GoodSource, "sdk: csharp\nfeatures:\n  auth.sign_in.sign_up: implemented\n");
        var (code, output) = Run(r => r.Check(), stale);
        code.Should().Be(CommandRunner.Failure);
        output.Should().Contain("out of sync").And.Contain("symbols differ");
    }

    [TestMethod]
    public void LocateReportsResolvedAndFailsOnUnresolved()
    {
        var resolvedCompliance = "sdk: csharp\nfeatures:\n  auth.sign_in.sign_up:\n    status: implemented\n    symbols:\n      - Client.SignUp\n";
        Run(r => r.Locate(), WorkspaceWith(GoodSource, resolvedCompliance)).Should()
            .Match<(int Code, string Output)>(t => t.Code == CommandRunner.Success && t.Output.Contains("1 resolved"));

        var missingCompliance = "sdk: csharp\nfeatures:\n  auth.sign_in.sign_up:\n    status: implemented\n    symbols:\n      - Client.Ghost\n";
        var (code, output) = Run(r => r.Locate(), WorkspaceWith(GoodSource, missingCompliance));
        code.Should().Be(CommandRunner.Failure);
        output.Should().Contain("MISS").And.Contain("Client.Ghost");
    }

    [TestMethod]
    public void IndexEmitsJsonForTheExtractedSurface()
    {
        var (code, output) = Run(r => r.Index(), WorkspaceWith(GoodSource));
        code.Should().Be(CommandRunner.Success);
        output.Should().Contain("\"QualifiedName\": \"Client.SignUp\"").And.Contain("auth.sign_in.sign_up");
    }
}
