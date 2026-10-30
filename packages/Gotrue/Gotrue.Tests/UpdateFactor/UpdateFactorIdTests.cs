#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.UpdateFactor;

#endregion

namespace Gotrue.Tests.UpdateFactor;

/// <summary>
///     The id checks made before a factor update leaves the SDK: both ids must be canonical UUIDs, the form the auth
///     server issues them in, so a typo never reaches the server as a 404 that reads like a missing record.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateFactorIdTests
{
    private const string Uuid = "2b306a77-21dc-4110-ba71-537cb56b9e98";

    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    private static readonly FactorChanges Rename = new() { FriendlyName = "Renamed" };

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-uuid")]
    [DataRow("2b306a7721dc4110ba71537cb56b9e98")]
    [DataRow("{2b306a77-21dc-4110-ba71-537cb56b9e98}")]
    [DataRow("x/../../users/2b306a77-21dc-4110-ba71-537cb56b9e98")]
    public async Task UpdateFactorAsync_ShouldThrowArgument_GivenUserIdThatIsNotCanonicalUuid(string userId)
    {
        var update = () => Admin.UpdateFactorAsync(userId, Uuid, Rename);
        await update.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName("userId", "the spec rejects a user id that is not a UUID before any request is sent");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-uuid")]
    [DataRow("2b306a7721dc4110ba71537cb56b9e98")]
    [DataRow("x/../../2b306a77-21dc-4110-ba71-537cb56b9e98")]
    public async Task UpdateFactorAsync_ShouldThrowArgument_GivenFactorIdThatIsNotCanonicalUuid(string factorId)
    {
        var update = () => Admin.UpdateFactorAsync(Uuid, factorId, Rename);
        await update.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName("factorId", "the spec rejects a factor id that is not a UUID before any request is sent");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldShowTheExpectedForm_GivenIdThatIsNotCanonicalUuid()
    {
        var update = () => Admin.UpdateFactorAsync(Uuid, "not-a-uuid", Rename);
        await update.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage("*canonical form, for example '????????-????-????-????-????????????'*", "the caller learns which form of id the SDK accepts without reading its source");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowArgumentNull_GivenNullUserId()
    {
        var update = () => Admin.UpdateFactorAsync(null!, Uuid, Rename);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("userId");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowArgumentNull_GivenNullFactorId()
    {
        var update = () => Admin.UpdateFactorAsync(Uuid, null!, Rename);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("factorId");
    }
}
