using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

[TestClass]
public class CliEntryPointTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CliEntryPoint.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void NoArgumentsPrintsUsage()
    {
        var (code, _, err) = Run();
        code.Should().Be(2);
        err.Should().Contain("Usage:");
    }

    [TestMethod]
    public void UnknownCommandFails()
    {
        var (code, _, err) = Run("frobnicate");
        code.Should().Be(2);
        err.Should().Contain("Unknown command");
    }

    [TestMethod]
    public void GenerateWithoutSpecFails()
    {
        var (code, _, err) = Run("generate", "--root", ".");
        code.Should().Be(2);
        err.Should().Contain("requires --spec");
    }

    [TestMethod]
    public void MissingComplianceFileIsHandledAsAnEnvironmentError()
    {
        var (code, _, err) = Run("locate", "--root", Path.GetTempPath(), "--compliance", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.yaml"));
        code.Should().Be(2);
        err.Should().Contain("❌");
    }

    [TestMethod]
    public void GenerateEndToEndThroughTheFilesystem()
    {
        var root = Directory.CreateTempSubdirectory("capmatrix");
        try
        {
            var pkg = Directory.CreateDirectory(Path.Combine(root.FullName, "packages", "Gotrue", "Gotrue"));
            File.WriteAllText(Path.Combine(pkg.FullName, "Client.cs"),
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
                """);

            var spec = Directory.CreateDirectory(Path.Combine(root.FullName, "spec"));
            File.WriteAllText(Path.Combine(spec.FullName, "auth.yaml"),
                "area: auth\ntitle: Authentication\ngroups:\n  - id: sign_in\n    title: Sign-in\nfeatures:\n  - id: auth.sign_in.sign_up\n    group: sign_in\n");

            var outPath = Path.Combine(root.FullName, "sdk-compliance.yaml");
            var (code, output, _) = Run("generate", "--root", root.FullName, "--spec", spec.FullName, "--out", outPath);

            code.Should().Be(0);
            output.Should().Contain("✅");
            File.ReadAllText(outPath).Should().Contain("auth.sign_in.sign_up:").And.Contain("- Client.SignUp");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
