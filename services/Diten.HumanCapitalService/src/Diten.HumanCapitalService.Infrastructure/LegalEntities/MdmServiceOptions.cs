namespace Diten.HumanCapitalService.Infrastructure.LegalEntities;

/// <summary>Binds the <c>MdmService</c> configuration section (legal-entity hierarchy source of truth).</summary>
public sealed class MdmServiceOptions
{
    public const string SectionName = "MdmService";

    /// <summary>Base URL of the Master Data service exposing <c>api/legal-entities</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>How long a resolved hierarchy snapshot is cached before MDM is queried again.</summary>
    public int CacheSeconds { get; set; } = 60;
}
