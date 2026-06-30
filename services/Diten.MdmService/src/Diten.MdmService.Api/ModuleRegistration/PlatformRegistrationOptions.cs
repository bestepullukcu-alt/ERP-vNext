namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// MC-3b-expand (Part B) — config for the MDM → Platform self-registration HTTP push (mirrors DevEnablement).
/// </summary>
public sealed class PlatformRegistrationOptions
{
    public const string SectionName = "PlatformRegistration";

    /// <summary>Direct S2S base URL of the Platform service (e.g. http://localhost:5057). /api/internal is not gateway-exposed.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Shared internal API key sent as X-Internal-Api-Key. Kept in appsettings.Development.json locally.</summary>
    public string InternalApiKey { get; set; } = string.Empty;
}
