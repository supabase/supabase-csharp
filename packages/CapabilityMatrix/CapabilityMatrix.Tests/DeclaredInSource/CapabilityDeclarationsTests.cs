using FluentAssertions;
using Supabase.CapabilityMatrix.Capabilities;
using Supabase.CapabilityMatrix.DeclaredInSource;

namespace Supabase.CapabilityMatrix.Tests.DeclaredInSource;

/// <summary>Reading the capabilities a member declares about itself from C# source.</summary>
[TestClass]
[TestCategory("Unit")]
public class CapabilityDeclarationsTests
{
    private static IReadOnlyList<DeclaredCapability> FromSample() =>
        CapabilityDeclarations.From([new SourceFile("packages/Gotrue/Gotrue/Client.cs", Fixtures.Read("sample-source.cs.txt"))]);

    [TestMethod]
    public void From_ShouldReadStatusNoteSymbolAndVisibility_GivenAnnotatedMembers() =>
        FromSample().Should().BeEquivalentTo(new[]
        {
            new DeclaredCapability("realtime.subscriptions.broadcast", CapabilityStatus.Implemented, null, "RealtimeBroadcast", FromPublicApi: true),
            new DeclaredCapability("auth.sign_in.sign_up", CapabilityStatus.Partial, "Session not adopted on a confirmations-off sign-up.", "Client.SignUp", FromPublicApi: true),
            new DeclaredCapability("auth.session.get_user", CapabilityStatus.Implemented, null, "Client.CurrentUser", FromPublicApi: true),
            new DeclaredCapability("client.authentication_integration.cross_client_token_sync", CapabilityStatus.Implemented, "Injected into sub-clients via a shared callback.", "Client.GetAuthHeaders", FromPublicApi: false),
            new DeclaredCapability("functions.invocation.invoke", CapabilityStatus.Implemented, "Three return shapes.", "Client.Invoke", FromPublicApi: true),
            new DeclaredCapability("client.request_configuration.global_headers", CapabilityStatus.Implemented, null, "Options.Headers", FromPublicApi: true),
            new DeclaredCapability("realtime.client.connect", CapabilityStatus.Implemented, null, "IRealtimeClient.Connect", FromPublicApi: true),
            new DeclaredCapability("database.using_filters.eq", CapabilityStatus.Implemented, null, "Constants.Operator.Equals", FromPublicApi: true),
        });
}
