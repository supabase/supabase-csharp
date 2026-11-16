using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.AccessToken;

/// <summary>
/// Drives the local Supabase stack with a client constructed with the anon key but authorized by
/// <see cref="ClientOptions.RetrieveAccessToken"/>: the server acts on the retrieved token, so a service-role
/// token reaches buckets the anon key cannot see and a malformed token is rejected.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class StorageWithRetrievedAccessTokenTests : StorageE2EFixture
{
    [TestMethod]
    public async Task GetBucket_ShouldReturnTheBucket_GivenRetrievedServiceRoleToken()
    {
        var id = await this.NewBucket();
        var bucket = await AnonClientRetrieving(Helpers.ServiceKey).GetBucket(id);
        bucket!.Id.Should().Be(id, "the anon key alone resolves a private bucket to null, so the retrieved token was sent");
    }

    [TestMethod]
    public async Task ListBuckets_ShouldThrow_GivenRetrievedMalformedToken()
    {
        var act = () => AnonClientRetrieving("not-a-jwt").ListBuckets();
        await act.Should().ThrowAsync<SupabaseStorageException>("the server verifies the retrieved token");
    }

    private static Client AnonClientRetrieving(string accessToken) =>
        new(Helpers.StorageUrl, new ClientOptions { RetrieveAccessToken = _ => Task.FromResult<string?>(accessToken) },
            new Dictionary<string, string> { { "Authorization", $"Bearer {Helpers.PublicKey}" } });
}
