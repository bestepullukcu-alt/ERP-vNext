namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

/// <summary>
/// MOD-0165 FU07 — the tenant's referenceable MDM legal entities, for the scope selector only.
/// <para><b>An authoring lookup is not a validation.</b> Picking an option here never substitutes for the per-id
/// fail-closed check that runs immediately before persistence: the list can be seconds out of date, and a period must
/// not be scoped to an entity that was deactivated while the form was open. This is the same separation the working
/// calendar draws between its legal-entity dropdown and its write-path validator.</para>
/// <para>Unreachable is a legitimate answer: <see cref="LegalEntityLookupResult.IsAvailable"/> is false and the list is
/// empty, so the UI can say "we could not load these" instead of showing an empty dropdown that looks like "there are
/// none". A hardcoded fallback list is forbidden.</para>
/// </summary>
public interface ICyclePeriodLegalEntityCatalog
{
    Task<LegalEntityLookupResult> GetReferenceableAsync(CancellationToken cancellationToken);
}

/// <summary>One selectable legal entity — id, code and display name, and nothing more: a picker is not a copy of MDM.</summary>
public sealed record LegalEntityLookupOption(Guid LegalEntityId, string Code, string DisplayName);

/// <summary><see cref="IsAvailable"/> distinguishes "MDM says the tenant has none" from "MDM did not answer".</summary>
public sealed record LegalEntityLookupResult(bool IsAvailable, IReadOnlyList<LegalEntityLookupOption> Options)
{
    public static readonly LegalEntityLookupResult Unavailable =
        new(false, Array.Empty<LegalEntityLookupOption>());
}
