using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.Buckets;

/// <summary>
/// End-to-end tests asserting the anon (public-key) client is denied bucket administration against a
/// running local Supabase stack: listing yields nothing, getting an existing bucket resolves to null,
/// and create/update/empty/delete all surface a <see cref="SupabaseStorageException"/>. Buckets are
/// provisioned with the service client through the fixture so they are always torn down.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class StorageBucketAnonTests : StorageE2EFixture
{
    [TestMethod]
    public async Task ListBuckets_ShouldBeEmptyAndDenyUpload()
    {
        (await this.PublicStorage.ListBuckets()).Should().BeEmpty();
        var id = await this.NewBucket();
        var act = () => this.PublicStorage.From(id).Upload(new byte[] { 0x0, 0x0, 0x0 }, "child/test.bin");
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task GetBucket_ShouldReturnNull_GivenNoPermission()
    {
        var id = await this.NewBucket();
        (await this.PublicStorage.GetBucket(id)).Should().BeNull();
    }

    [TestMethod]
    public async Task CreateBucket_ShouldThrow_GivenNoPermission()
    {
        var act = () => this.PublicStorage.CreateBucket("anon-create-should-be-denied");
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task UpdateBucket_ShouldThrow_GivenNoPermission()
    {
        var id = await this.NewBucket();
        var act = () => this.PublicStorage.UpdateBucket(id, new BucketUpsertOptions { Public = true });
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task EmptyBucket_ShouldThrow_GivenNoPermission()
    {
        var id = await this.NewBucket();
        var act = () => this.PublicStorage.EmptyBucket(id);
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task DeleteBucket_ShouldThrow_GivenNoPermission()
    {
        var id = await this.NewBucket();
        var act = () => this.PublicStorage.DeleteBucket(id);
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task PurgeProjectCacheAsync_ShouldThrow_GivenNoPermission()
    {
        var act = () => this.PublicStorage.PurgeProjectCacheAsync();
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode.Should().Be(403);
    }
}
