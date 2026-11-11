#region

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     The live stack's local mail server (Mailpit, <c>[local_smtp]</c> in <c>supabase/config.toml</c>): reads the auth
///     emails sent to a test's own address and follows their verification link the way a user's click would, without
///     following the redirect, so a test can read what the auth server hands back to the app.
/// </summary>
internal static class Mailbox
{
    private static readonly string Url = Environment.GetEnvironmentVariable("SUPABASE_CLI_MAILPIT_URL") ?? "http://127.0.0.1:54324";
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly Regex VerificationLink = new(@"https?://\S+/verify\?[^\s)""]+", RegexOptions.Compiled);
    private static readonly HttpClient Http = new();
    private static readonly HttpClient Browser = new(new HttpClientHandler { AllowAutoRedirect = false });

    /// <summary>
    ///     Clicks the verification link of the email sent to <paramref name="email" /> and returns the PKCE authorization
    ///     code the auth server appends to the redirect.
    /// </summary>
    internal static async Task<string> ClickForAuthCodeAsync(string email)
    {
        var link = await WaitForVerificationLinkAsync(email).ConfigureAwait(false);
        using var response = await Browser.GetAsync(link).ConfigureAwait(false);
        return HttpUtility.ParseQueryString(response.Headers.Location!.Query)["code"]
               ?? throw new InvalidOperationException($"The verification redirect carried no code: {response.Headers.Location}");
    }

    private static async Task<string> WaitForVerificationLinkAsync(string email)
    {
        var deadline = DateTimeOffset.UtcNow + DeliveryTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var messageId = await FindMessageIdAsync(email).ConfigureAwait(false);
            if (messageId != null)
            {
                return await ReadVerificationLinkAsync(messageId).ConfigureAwait(false);
            }
            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
        throw new TimeoutException($"No email reached {email} within {DeliveryTimeout}.");
    }

    private static async Task<string?> FindMessageIdAsync(string email)
    {
        using var found = JsonDocument.Parse(await Http.GetStringAsync($"{Url}/api/v1/search?query={Uri.EscapeDataString($"to:\"{email}\"")}").ConfigureAwait(false));
        return found.RootElement.GetProperty("messages").EnumerateArray().Select(message => message.GetProperty("ID").GetString()).FirstOrDefault();
    }

    private static async Task<string> ReadVerificationLinkAsync(string messageId)
    {
        using var message = JsonDocument.Parse(await Http.GetStringAsync($"{Url}/api/v1/message/{messageId}").ConfigureAwait(false));
        return VerificationLink.Match(message.RootElement.GetProperty("Text").GetString()!).Value;
    }
}
