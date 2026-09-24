using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Resend;

/// <summary>
/// Parameters for the Resend API.
/// </summary>
public class ResendParameters
{
    /// <summary>
    /// The email address to resend to.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// The phone number to resend to.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// The type of resend.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Additional options to customize the behavior of the Resend API.
    /// Provides support for features such as CAPTCHA tokens and email redirection.
    /// </summary>
    [JsonPropertyName("options")]
    public ResendOptions? Options { get; set; }

    /// <summary>
    /// Represents the parameters used for the Resend API, allowing for resending
    /// confirmation codes or links based on the provided inputs. This includes
    /// support for email, phone, and custom data.
    /// </summary>
    public ResendParameters(ResendType type) =>
        this.Type = Core.Helpers.GetMappedToAttr(type).Mapping;
}
