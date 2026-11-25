using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.PutVectors;

namespace Storage.Tests.PutVectors;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).Index(...).PutVectorsAsync</c> puts on the wire; the approved body must
/// match the service's <c>PutVectors</c> schema (<c>vectorBucketName</c>, <c>indexName</c> and <c>vectors</c>, each with its key, <c>float32</c> data and optional metadata, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PutVectorsApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task PutVectorsRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        var metadata = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement("Intro"),
            ["tags"] = JsonSerializer.SerializeToElement(new[] { "a", "b" }),
            ["pages"] = JsonSerializer.SerializeToElement(12),
        };
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").PutVectorsAsync(new[]
        {
            new NewVector { Key = "doc-1", Data = new[] { 0.5f, 0.25f }, Metadata = metadata },
            new NewVector { Key = "doc-2", Data = new[] { 1f, 2f } },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
