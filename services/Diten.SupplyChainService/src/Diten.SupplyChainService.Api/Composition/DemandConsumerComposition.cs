namespace Diten.SupplyChainService.Api.Composition;

/// <summary>The demand plan a non-production host accepts until DEMAND v2 ships a real reader.</summary>
public sealed record DemandFixtureSeed(Guid TenantId, Guid LegalEntityId, string PlanId, string Version, string Checksum);

/// <summary>
/// G4 (Wave 6.1, owner decisions 2026-10-18): S&amp;OP (MOD-0190) and Capacity (MOD-0192) are composed ONLY where a
/// demand source exists. Until DEMAND v2, that source is a configured fixture seed (<c>DemandFixtures</c>), which only
/// non-production configuration may carry. Production never composes them on a fixture — not even if the section is
/// present — so its routes answer an honest 404 rather than a module that rejects every create (K4, Program.cs note).
/// </summary>
public static class DemandConsumerComposition
{
    public const string SectionName = "DemandFixtures";

    /// <summary>The seed to compose with, or null when S&amp;OP/Capacity must stay uncomposed.</summary>
    public static DemandFixtureSeed? SeedFor(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsProduction()) return null;
        var section = configuration.GetSection(SectionName);
        if (!bool.TryParse(section["Enabled"], out var enabled) || !enabled) return null;

        // Enabled but incomplete is a configuration error, not a reason to compose a module that refuses everything.
        if (!Guid.TryParse(section["TenantId"], out var tenant) || tenant == Guid.Empty
            || !Guid.TryParse(section["LegalEntityId"], out var legalEntity) || legalEntity == Guid.Empty)
        {
            throw new InvalidOperationException($"{SectionName} is enabled but TenantId/LegalEntityId are not non-empty UUIDs.");
        }

        string Required(string key) => string.IsNullOrWhiteSpace(section[key])
            ? throw new InvalidOperationException($"{SectionName}:{key} is required when {SectionName} is enabled.")
            : section[key]!;

        return new DemandFixtureSeed(tenant, legalEntity, Required("PlanId"), Required("Version"), Required("Checksum"));
    }
}
