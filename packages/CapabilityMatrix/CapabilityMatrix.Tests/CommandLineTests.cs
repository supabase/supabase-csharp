using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

/// <summary>Dispatching the generate and validate use cases and reporting their outcome.</summary>
[TestClass]
[TestCategory("Unit")]
public class CommandLineTests
{
    private static ExitCode Run(params string[] arguments) =>
        CommandLine.Run(arguments, TextWriter.Null, TextWriter.Null);

    [TestMethod]
    public void Run_ShouldReturnUsageError_GivenNoCommand() =>
        Run().Should().Be(ExitCode.UsageError);

    [TestMethod]
    public void Run_ShouldReturnUsageError_GivenAnUnknownCommand() =>
        Run("frobnicate").Should().Be(ExitCode.UsageError);

    [TestMethod]
    public void Run_ShouldReturnUsageError_GivenGenerateWithoutASpec() =>
        Run("generate").Should().Be(ExitCode.UsageError);

    [TestMethod]
    public void Run_ShouldReturnUsageError_GivenAMissingSpecDirectory() =>
        Run("generate", "--spec", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Should().Be(ExitCode.UsageError);

    [TestMethod]
    public void Run_ShouldWriteTheComplianceFile_GivenGenerateOnASampleRepository()
    {
        using var repository = SampleRepository.Create();
        Run("generate", "--spec", repository.Spec);
        File.ReadAllText(repository.Output).Should().Contain("auth.sign_in.sign_up:");
    }

    [TestMethod]
    public void Run_ShouldReturnSuccess_GivenValidateOnACleanRepository()
    {
        using var repository = SampleRepository.Create();
        Run("validate", "--spec", repository.Spec).Should().Be(ExitCode.Success);
    }

    [TestMethod]
    public void Run_ShouldReturnProblemsFound_GivenValidateOnADriftedRepository()
    {
        using var repository = SampleRepository.Drifted();
        Run("validate", "--spec", repository.Spec).Should().Be(ExitCode.ProblemsFound);
    }

    [TestMethod]
    public void Run_ShouldSummariseWhatItChecked_GivenValidateOnACleanRepository()
    {
        using var repository = SampleRepository.Create();
        var output = new StringWriter();
        CommandLine.Run(["validate", "--spec", repository.Spec], output, TextWriter.Null);
        output.ToString().Should().Contain("Validated").And.Contain("matrix features");
    }

    [TestMethod]
    public void Run_ShouldReportEachProblem_GivenValidateOnADriftedRepository()
    {
        using var repository = SampleRepository.Drifted();
        var error = new StringWriter();
        CommandLine.Run(["validate", "--spec", repository.Spec], TextWriter.Null, error);
        error.ToString().Should().Contain("in the capability annotations:");
    }
}
