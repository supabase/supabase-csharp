#region

using System;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.RenamePasskey;

/// <summary>
///     Pins the exact bytes a passkey rename puts on the wire: the new name as <c>friendly_name</c>, the only field
///     PATCH /passkeys/{passkey_id} reads.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RenamePasskeyApprovalTests : RequestApprovalFixture
{
    private const string PasskeyPath = "/passkeys/5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24";
    private const string RenamedPasskey = """{"id":"5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24","friendly_name":"Work laptop","created_at":"2026-03-02T09:15:30Z"}""";
    private static readonly Guid PasskeyId = Guid.Parse("5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24");

    [TestInitialize]
    public void AnswerWithRenamedPasskey() => this.RespondTo(PasskeyPath, RenamedPasskey);

    [TestMethod]
    public async Task RenamePasskeyRequest_ShouldSendTheNewFriendlyName()
    {
        await this.Api.RenamePasskeyAsync("user-jwt", PasskeyId, "Work laptop");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
