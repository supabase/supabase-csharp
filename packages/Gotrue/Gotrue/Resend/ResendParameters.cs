using System.Collections.Generic;

namespace Supabase.Gotrue.Resend;

/// <summary>
/// Parameters for the Resend API.
/// </summary>
public class ResendParameters
{
    /// <summary>
    /// The email address to resend to.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    /// The phone number to resend to.
    /// </summary>
    public string? Phone { get; }

    /// <summary>
    /// The type of resend.
    /// </summary>
    public ResendType Type { get; }

    /// <summary>
    /// Additional options to customize the behavior of the Resend API.
    /// Provides support for features such as CAPTCHA tokens and email redirection.
    /// </summary>
    public ResendOptions? Options { get; }

    /// <summary>
    /// Represents the parameters required for a resend operation, such as resending a confirmation email
    /// or a verification SMS, depending on the specified type.
    /// </summary>
    public ResendParameters(ResendType type, string? email, string? phone, ResendOptions? options)
    {
        this.Type = type;
        this.Email = email;
        this.Phone = phone;
        this.Options = options;
    }

    /// <summary>
    /// Converts the resend parameters to a dictionary format, including the type, email, phone,
    /// and any additional options, for use in API requests.
    /// </summary>
    /// <returns>
    /// A dictionary containing the key-value pairs representing the resend parameters.
    /// </returns>
    public Dictionary<string, object> ToDictionary()
    {
        var body = new Dictionary<string, object>
        {
            { "type", Core.Helpers.GetMappedToAttr(this.Type).Mapping },
        };

        body = this.AddEmailAndTokenField(body);
        body = this.AddPhoneField(body);

        return body;
    }

    internal string ApplyRedirectTo(string url)
    {
        var newUrl = $"{url}/resend";
        if (!this.IsEmail())
            return newUrl;

        if (!string.IsNullOrWhiteSpace(this.Options?.EmailRedirectTo))
            newUrl = $"{url}/resend?redirect_to={this.Options.EmailRedirectTo}";

        return newUrl;
    }

    private bool IsEmail() => this.Type is ResendType.EmailChange or ResendType.SignUp;

    private Dictionary<string, object> AddEmailAndTokenField(Dictionary<string, object> parameter)
    {
        if (!this.IsEmail())
            return parameter;

        var body = new Dictionary<string, object>(parameter);
        if (!string.IsNullOrWhiteSpace(this.Email))
            body.Add("email", this.Email);

        if (!string.IsNullOrWhiteSpace(this.Options?.CaptchaToken))
            body.Add(
                "gotrue_meta_security",
                new Dictionary<string, string> { { "captcha_token", this.Options.CaptchaToken } }
            );

        return body;
    }

    private Dictionary<string, object> AddPhoneField(Dictionary<string, object> parameter)
    {
        if (this.IsEmail())
            return parameter;

        var body = new Dictionary<string, object>(parameter);
        if (!string.IsNullOrWhiteSpace(this.Phone))
            body.Add("phone", this.Phone);

        return body;
    }
}
