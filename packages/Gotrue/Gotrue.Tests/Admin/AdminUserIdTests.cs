#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Ids that no escaping can keep on their own route — <c>.</c> and <c>..</c>, which <see cref="System.Uri" />
///     resolves into the parent route — and a missing id are rejected before the request leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AdminUserIdTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    public async Task GetUserById_ShouldThrowArgumentNull_GivenNullId()
    {
        var get = () => Admin.GetUserById(null!);
        await get.Should().ThrowExactlyAsync<ArgumentNullException>()
            .WithParameterName("userId", "the exception names the argument the caller passed, not an internal one");
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task GetUserById_ShouldThrowArgument_GivenDotSegmentId(string id)
    {
        var get = () => Admin.GetUserById(id);
        await get.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage("*cannot be '.' or '..'*", "System.Uri resolves a dot segment into the parent route, even percent-encoded")
            .WithParameterName("userId");
    }
}
