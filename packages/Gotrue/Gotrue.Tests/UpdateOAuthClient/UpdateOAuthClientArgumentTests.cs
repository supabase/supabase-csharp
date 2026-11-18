#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.UpdateOAuthClient;

#endregion

namespace Gotrue.Tests.UpdateOAuthClient;

/// <summary>
///     The argument checks made before an OAuth client update leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateOAuthClientArgumentTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    public static IEnumerable<object[]> ChangesThatChangeNothing =>
        new[]
        {
            new object[] { new OAuthClientChanges() },
            new object[] { new OAuthClientChanges { Options = new OAuthClientOptions() } },
        };

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowArgumentNull_GivenNullChanges()
    {
        var update = () => Admin.UpdateOAuthClientAsync(Guid.NewGuid(), null!);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("changes");
    }

    [TestMethod]
    [DynamicData(nameof(ChangesThatChangeNothing))]
    public async Task UpdateOAuthClientAsync_ShouldThrowArgument_GivenNoMemberSet(OAuthClientChanges changes)
    {
        var update = () => Admin.UpdateOAuthClientAsync(Guid.NewGuid(), changes);
        (await update.Should().ThrowExactlyAsync<ArgumentException>().WithMessage("*Set at least one member*"))
            .And.ParamName.Should().Be("changes", "the auth server answers 400 'No fields provided for update' to a request that changes nothing");
    }
}
