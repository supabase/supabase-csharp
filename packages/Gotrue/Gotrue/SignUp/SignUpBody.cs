using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.SignUp;

internal sealed record SignUpBody
{
    private const string MissingPhone = "Phone number not provided.";

    private SignUpBody(string password, SignUpOptions? options)
    {
        this.Password = password;
        this.Data = options?.Data;
        this.Security = string.IsNullOrEmpty(options?.CaptchaToken) ? null : new SecurityBody(options!.CaptchaToken!);
    }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; private init; }

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; private init; }

    [JsonPropertyName("password")]
    public string Password { get; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Data { get; }

    [JsonPropertyName("gotrue_meta_security")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SecurityBody? Security { get; }

    [JsonPropertyName("channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Channel { get; private init; }

    [JsonPropertyName("code_challenge")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CodeChallenge { get; private init; }

    [JsonPropertyName("code_challenge_method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CodeChallengeMethod { get; private init; }

    internal static SignUpBody ForEmail(string email, string password, SignUpOptions? options, string? codeVerifier) =>
        new SignUpBody(password, options) { Email = email }.WithCodeChallenge(codeVerifier);

    internal static SignUpBody ForPhone(string phone, string password, SignUpOptions? options) =>
        string.IsNullOrEmpty(phone)
            ? throw new GotrueException(MissingPhone, FailureHint.Reason.UserBadPhoneNumber)
            : new SignUpBody(password, options) { Phone = phone, Channel = ChannelOf(options) };

    private SignUpBody WithCodeChallenge(string? codeVerifier) =>
        codeVerifier == null
            ? this
            : this with { CodeChallenge = Helpers.GeneratePKCENonceVerifier(codeVerifier), CodeChallengeMethod = "s256" };

    private static string ChannelOf(SignUpOptions? options) =>
        Core.Helpers.GetMappedToAttr(options?.Channel ?? SignInWithPasswordlessPhoneOptions.MessagingChannel.SMS).Mapping;

    internal sealed record SecurityBody([property: JsonPropertyName("captcha_token")] string CaptchaToken);
}
