using Supabase.Core.Attributes;

[assembly: NotApplicableCapability(
    "client.authentication_integration.session_url_detection",
    "Redirect detection reads the browser's current URL (window.location), which a .NET process does not have. Apps receive the OAuth callback through their own route, WebAuthenticator or NavigationManager and complete it explicitly with Auth.GetSessionFromUrl(Uri) or Auth.ExchangeCodeForSession(verifier, code).")]
