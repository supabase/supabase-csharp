using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime.PostgresChanges;

namespace Realtime.Tests.PostgresChanges;

/// <summary>
///     Hydration of a postgres_changes frame into a model (realtime-csharp#35): <c>Model&lt;T&gt;</c> and
///     <c>OldModel&lt;T&gt;</c> deserialize the record, and when a <c>PostgrestClient</c> is configured they
///     attach its context so <c>Update</c>/<c>Delete</c> can be called on the returned model. This reproduces
///     the exact deserialization path <c>RealtimeChannel.HandleSocketMessage</c> uses, without a live socket.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PostgresChangesModelTests
{
    private const string BaseUrl = "http://localhost:54321/rest/v1";

    private static PostgresChangesResponse BuildResponse(IPostgrestClient? postgrestClient)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PostgresChangesUpdateEvent.json"));
        var settings = Wire.Settings();
        var response = JsonSerializer.Deserialize<PostgresChangesResponse>(json, settings)!;
        response.Json = json;
        response.SerializerSettings = settings;
        response.PostgrestClient = postgrestClient;
        return response;
    }

    [TestMethod]
    public void Model_ShouldDeserialiseRecord()
    {
        var model = BuildResponse(postgrestClient: null).Model<Todo>();
        model.Should().NotBeNull();
        model!.Id.Should().Be(12);
        model.Details.Should().Be("test...");
    }

    [TestMethod]
    public void OldModel_ShouldDeserialiseOldRecord()
    {
        var model = BuildResponse(postgrestClient: null).OldModel<Todo>();
        model.Should().NotBeNull();
        model!.Details.Should().Be("previous");
    }

    [TestMethod]
    public void Model_ShouldLeaveClientContextNull_GivenNoPostgrestClient()
    {
        var model = BuildResponse(postgrestClient: null).Model<Todo>()!;
        model.BaseUrl.Should().BeNull();
        model.RequestClientOptions.Should().BeNull();
    }

    [TestMethod]
    public void Model_ShouldAttachClientContext_GivenPostgrestClient()
    {
        var model = BuildResponse(new Supabase.Postgrest.Client(BaseUrl)).Model<Todo>()!;
        model.BaseUrl.Should().Be(BaseUrl);
        model.RequestClientOptions.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Update_ShouldThrowLoudly_GivenNoClientContext()
    {
        var model = BuildResponse(postgrestClient: null).Model<Todo>()!;
        var act = () => model.Update<Todo>();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Message.Should().Contain("BaseUrl");
    }

    /// <summary>
    ///     supabase-csharp#486: a record column named like a <c>SocketResponsePayload</c> member (here a numeric
    ///     <c>type</c> vs. the payload's string <c>ActionType</c>) used to fail the strict System.Text.Json token
    ///     check while decoding the frame, so <c>RealtimeChannel.HandleSocketMessage</c> dropped the event.
    /// </summary>
    [TestMethod]
    public void Decode_ShouldNotFail_GivenRecordColumnCollidesWithPayloadMember()
    {
        const string json =
            "{\"topic\":\"realtime:public:things\",\"event\":\"postgres_changes\",\"payload\":{\"data\":{" +
            "\"schema\":\"public\",\"table\":\"things\",\"type\":\"INSERT\"," +
            "\"record\":{\"id\":1,\"type\":2,\"name\":\"first\"}}," +
            "\"ids\":[1]},\"ref\":null}";
        var settings = Wire.Settings();

        var response = JsonSerializer.Deserialize<PostgresChangesResponse>(json, settings)!;
        response.Json = json;
        response.SerializerSettings = settings;

        response.Payload!.Data.Should().NotBeNull();

        var model = response.Model<Thing>()!;
        model.Id.Should().Be(1);
        model.Type.Should().Be(2);
        model.Name.Should().Be("first");
    }
}
