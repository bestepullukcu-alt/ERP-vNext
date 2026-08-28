using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations below are the contract a
/// consumer can rely on: they say out loud that a period produces nothing, that no job ever moves it, and that the
/// working-day question belongs to someone else.
/// </summary>
public sealed class GetCyclePeriodContractHandler
    : IRequestHandler<GetCyclePeriodContractQuery, Response<CyclePeriodContractDto>>
{
    public const string ModuleId = "MOD-0165-FU07";
    public const string ModuleName = "Cycle Period";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU07-cycle-period (the tenant's named planning PERIOD master: code, name, planning year, sequence in year, an "
        + "inclusive start/end day window, a DISCRIMINATED scope - tenant / country / legal-entity / business-unit, "
        + "exactly one reference per period - and a draft/active/closed lifecycle; create / read / update / activate / "
        + "close, an active-overlap ban enforced per scope at activate, a scope-options read for the cascading "
        + "selector, and a read-only resolution seam walking business-unit > legal-entity > country > tenant and "
        + "answering resolved / none / ambiguous). FU07 widened WHERE a period lives and nothing else: NO MicroTarget "
        + "row, campaign binding, VisitFrequencyPolicy write, strategy apply, working-day calculation, cycle-calendar "
        + "hierarchy, version clone, auto-close job, reschedule, hard delete or bulk delete is opened. MOD-0155, "
        + "MOD-0165 FU03/FU04, MOD-0167 FU04, MOD-0151 Territory and MDM are untouched - Territory and MDM are READ "
        + "only, to narrow a picker and to prove a reference before persistence.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a CyclePeriod is a PERIOD MASTER and produces nothing: no MicroTarget row, no CampaignTarget, no VisitFrequencyPolicy and no plan of any kind is created by any endpoint here. Applying a plan to a period is MOD-0155 (MicroTarget), and there is deliberately no /apply, /generate and no write path into another module's aggregate",
        "this is NOT a working calendar. Whether a given DAY is a working day or a public holiday belongs to the platform working-calendar capability, and nothing here counts working days, skips weekends or reads a holiday provider; combining the two ('how many working days does this period have?') is the consumer's job (follow-up F-CALENDAR-DAYS)",
        "the period's own StartDate/EndDate ARE its effective window; there is no second EffectiveFrom/EffectiveTo pair, because two date pairs would be two truths and a consumer could not tell which one resolve-active honours. EndDate is INCLUSIVE and both ends are normalised to UTC midnight",
        "scope is DISCRIMINATED and scope is IDENTITY: a period lives at exactly ONE address (tenant / country / legal-entity / business-unit) and carries exactly the one reference that level needs. A combination is refused rather than ignored, because 'most specific wins' needs a total order and a combination only yields a lattice. ScopeType is IMMUTABLE after creation at every status, draft included - a period at the wrong address is closed and a new one is opened (a draft may still correct its scope REFERENCE, which is a different act)",
        "ACTIVE periods of the SAME (ScopeType, ScopeRef) scope may never share a day: activate answers 409 and the row stays draft. DRAFT periods may overlap freely (that is the planning space) and CLOSED periods block nothing. There is deliberately no 'only one active row' rule, which would make planning a whole year impossible",
        "periods at DIFFERENT scope levels MAY overlap, and must be allowed to: a country calendar and a business unit's own calendar covering the same days is exactly the situation precedence exists to decide, so banning cross-level overlap would make the resolver's fallback unreachable. Do not assume 'no two active periods ever share a day' - assume it per scope",
        "resolve-active answers resolved / none / ambiguous and never guesses: with no covering period it returns none (not the nearest period, not a default), and with more than one it returns ambiguous with the candidate ids rather than picking a winner - that state only exists when the overlap ban was bypassed, and hiding it behind a plausible answer would hide a data defect",
        "resolution walks business-unit > legal-entity > country > tenant and answers from the FIRST level that has a covering row, never merging two levels. A level the caller did not name is SKIPPED, which is what keeps an FU06-shaped call (an instant plus a business unit) answering exactly what FU06 answered however many country or legal-entity periods exist. A level that answers STOPS the walk - including when it answers ambiguous, because stepping over a broken level would hide the defect. ResolvedScopeType names the level that actually answered, and it is informational: learning that your business unit has no period of its own is not a licence to create one, since this seam writes nothing",
        "there is FALLBACK but no INHERITANCE: a broader level's period is returned as that level's period (ResolvedScopeType says so), never re-labelled as the caller's own",
        "time never mutates a row: there is no scheduler and no auto-close, so an active period whose window has passed simply stops resolving until an operator closes it. Closing is an explicit act and it frees those days for a new active period in the same scope",
        "closed is TERMINAL: there is no reopen endpoint, because plans, visits and reports already point at a period by id and re-opening one would retroactively change what a past plan meant. An active period's dates, year, sequence and business unit are immutable (name and description stay editable); correcting a live calendar means closing it and opening a new period (follow-up F-RESCHEDULE)",
        "there is no version lineage and no new-version clone (unlike MOD-0167 FU04 StrategyTemplate): a period is a calendar fact rather than an authored play, and cloning it would fork the history its consumers already reference by id",
        "VisitFrequencyPolicy.CycleId has NO master here and is not pretended to have one. This FU makes CyclePeriodId resolvable and stops there; whether a cycle-calendar hierarchy is needed at all is follow-up F-CYCLE-CALENDAR. This FU also does not FK-check VisitFrequencyPolicy.CyclePeriodId, because that would mean changing MOD-0165 FU03 (follow-up F-VFP-FK)",
        "Campaign is untouched: no CyclePeriodId field is added to it, and a campaign's own start/end window stays that campaign's window rather than becoming a period (follow-up F-CAMPAIGN-BIND)",
        "vocabulary is IN-DOMAIN for STRUCTURE and GOVERNED for REFERENCES: statuses and scope types are validated against the runtime's own constants (they change what the engine does, so a tenant cannot extend them), while the scope references are validated against published MOD-0048 sets - the country against COUNTRY_CODES and the business unit against the same business-unit set MOD-0151 Territory uses. An unknown status or scope type is refused (400) rather than quietly treated as draft/tenant or ignored as a filter, and an unpublished reference SET is reported as a different failure from an unknown VALUE, because one is fixed by an operator and the other by retyping. No hardcoded fallback list exists anywhere",
        "the business unit is NO LONGER an opaque string (FU07): it must be a published business-unit value. The selector NARROWS that list to the units the tenant's ACTIVE territory plans cover for the chosen country and window, but the narrowing is advisory - a valid code outside it is accepted and stamped BusinessUnitSource=manual. Making the territory list a hard gate would pin a period's identity to MOD-0151's lifecycle, so superseding a plan would make an existing period uneditable and a period could not be planned before its field plan existed",
        "MDM is consulted for a legal-entity scope BEFORE anything is persisted and is never cached: an entity that answers 'no such entity / not active / not referenceable' is a 400, while a timeout, 5xx, auth rejection or malformed body is a 503 with nothing written. A 403 is deliberately NOT read as 'no such entity' - the entity may well exist and we were simply not allowed to look, and the caller needs mdm.legal-entities.read (follow-up F-MDM-PERM). No read path (list, detail, selector, resolve) ever calls MDM, so an outage cannot stop a tenant reading its own calendar",
        "RBAC keys crm.cycle-period.{read,manage,activate} are DEFINED but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up F-RBAC), under which activate collapses onto manage so the SoD cannot be enforced in dev. close shares the activate key on purpose - putting a period live and ending it are the same governance responsibility",
        "there is no DELETE, no PATCH and no bulk-delete endpoint anywhere; ending a period is the closed lifecycle, and TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetCyclePeriodContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<CyclePeriodContractDto>> Handle(
        GetCyclePeriodContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<CyclePeriodContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new CyclePeriodContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            CyclePeriodFeatureFlags.Current,
            CyclePeriodVocabularyDto.Current,
            CyclePeriodSupportedFilters.Current,
            CyclePeriodContractLimits.Current,
            CyclePeriodErrorCodes.All,
            CyclePeriodPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<CyclePeriodContractDto>.Success(dto));
    }
}
