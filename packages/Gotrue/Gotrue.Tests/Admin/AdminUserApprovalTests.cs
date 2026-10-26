#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Pins the exact bytes the admin user-management requests put on the wire. The attribute DTOs
///     (<see cref="AdminUserAttributes" />, <see cref="UserAttributes" />) and
///     <see cref="GenerateLinkOptions" /> serialize a mix of populated fields, explicit nulls, empty metadata
///     bags and enum mappings — the exact serialization surface the System.Text.Json migration changes.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminUserApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task CreateUserRequest_ShouldSerializeAllAttributes_GivenEmailAndPassword()
    {
        await this.Api.CreateUser("service-jwt",
            new AdminUserAttributes { Email = "user@example.com", Password = "super-secret-password" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateUserByIdRequest_ShouldSerializeAllAttributes_GivenEmail()
    {
        await this.Api.UpdateUserById("service-jwt", "user-id-123",
            new UserAttributes { Email = "new@example.com" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task InviteUserRequest_ShouldSerializeToExpectedPayload_GivenEmail()
    {
        await this.Api.InviteUserByEmail("user@example.com", "service-jwt");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task InviteUserRequest_ShouldSendRedirectToIntact_GivenRedirectWithOwnQuery()
    {
        await this.Api.InviteUserByEmail("user@example.com", "service-jwt", new InviteUserByEmailOptions { RedirectTo = RedirectWithOwnQuery });
        this.ValidateQueryParameters("redirect_to", RedirectWithOwnQuery);
    }

    [TestMethod]
    public async Task GenerateLinkRequest_ShouldSendRedirectToIntact_GivenRedirectWithOwnQuery()
    {
        await this.Api.GenerateLink("service-jwt",
            new GenerateLinkOptions(GenerateLinkOptions.LinkType.MagicLink, "user@example.com") { RedirectTo = RedirectWithOwnQuery });
        this.ValidateQueryParameters("redirect_to", RedirectWithOwnQuery);
    }

    [TestMethod]
    public async Task GenerateLinkRequest_ShouldSerializeToExpectedPayload_GivenMagicLinkType()
    {
        await this.Api.GenerateLink("service-jwt",
            new GenerateLinkOptions(GenerateLinkOptions.LinkType.MagicLink, "user@example.com"));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
