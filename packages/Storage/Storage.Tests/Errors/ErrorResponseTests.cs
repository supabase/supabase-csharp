using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.Errors;

/// <summary>
/// Covers <see cref="ErrorResponse.TryParse"/>: a JSON error body yields the parsed status and
/// message, while a non-JSON body (a gateway or plain-text error) returns null so callers fall back
/// to the raw content and status instead of letting a parse error escape.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ErrorResponseTests
{
    [TestMethod]
    public void TryParse_ShouldReturnNull_GivenNonJsonBody() =>
        ErrorResponse.TryParse("Upload failed: gateway error").Should().BeNull();

    [TestMethod]
    public void TryParse_ShouldReadStatusMessageAndCode_GivenJsonBody()
    {
        var parsed = ErrorResponse.TryParse(
            "{\"statusCode\":\"404\",\"error\":\"not_found\",\"code\":\"NoSuchKey\",\"message\":\"Object not found\"}");
        using (new AssertionScope())
        {
            parsed.Should().NotBeNull();
            parsed!.StatusCode.Should().Be(404);
            parsed.Message.Should().Be("Object not found");
            parsed.Code.Should().Be("NoSuchKey");
        }
    }

    [TestMethod]
    public void TryParse_ShouldLeaveCodeNull_GivenCodeMissing()
    {
        var parsed = ErrorResponse.TryParse(
            "{\"statusCode\":\"404\",\"error\":\"not_found\",\"message\":\"Object not found\"}");
        using (new AssertionScope())
        {
            parsed.Should().NotBeNull();
            parsed!.Code.Should().BeNull();
        }
    }

    [TestMethod]
    public void TryParse_ShouldReadMessageAndTypeAsCode_GivenIcebergEnvelope()
    {
        var parsed = ErrorResponse.TryParse(
            "{\"error\":{\"message\":\"Catalog name \\\"x\\\" not found\",\"type\":\"NoSuchCatalog\",\"code\":404}}");
        using (new AssertionScope())
        {
            parsed.Should().NotBeNull();
            parsed!.Message.Should().Be("Catalog name \"x\" not found", "the Iceberg routes nest the message under error");
            parsed.Code.Should().Be("NoSuchCatalog", "the Iceberg routes name the failure in error.type");
        }
    }

    [TestMethod]
    public void TryParse_ShouldKeepTopLevelFields_GivenErrorIsAString()
    {
        var parsed = ErrorResponse.TryParse(
            "{\"statusCode\":\"404\",\"error\":\"not_found\",\"code\":\"NoSuchKey\",\"message\":\"Object not found\"}");
        using (new AssertionScope())
        {
            parsed!.Code.Should().Be("NoSuchKey");
            parsed.Message.Should().Be("Object not found");
        }
    }

    [TestMethod]
    public void TryParse_ShouldReturnNull_GivenNonObjectJson() =>
        ErrorResponse.TryParse("[1]").Should().BeNull("a JSON array is not an error body");
}
