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
}
