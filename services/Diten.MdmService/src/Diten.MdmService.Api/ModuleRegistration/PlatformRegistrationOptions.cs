namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// MC-3b-expand (Part B) — config for the MDM → Platform self-registration HTTP push (mirrors DevEnablement).
/// </summary>
public sealed class PlatformRegistrationOptions
{
    public const string SectionName = "PlatformRegistration";

    /// <summary>Direct S2S base URL of the Platform service (e.g. http://localhost:5057). /api/internal is not gateway-exposed.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Legacy shared key for unrelated internal integrations; never a module-registration fallback.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    /// <summary>Non-secret deployment-provisioned identifier for the MDM module-registration credential.</summary>
    public string ModuleRegistrationCredentialIdentifier { get; set; } = string.Empty;

    /// <summary>Secret supplied only through secure configuration/environment/secret-store binding.</summary>
    public string ModuleRegistrationCredentialSecret { get; set; } = string.Empty;
}
