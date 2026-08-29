using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContactAvailability;
using Diten.CrmService.Application.Features.ContactAvailability.Handlers;
using Diten.CrmService.Application.Features.ContactAvailability.Queries;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using ContactEntity = Diten.CrmService.Domain.Entities.Contact;

namespace Diten.CrmService.Application.Features.Territory.Readiness;

internal sealed class TerritoryReadinessReader
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;
    private readonly IAccountTerritoryAssignmentRepository _coverage;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryResourceAssignmentRepository _resources;
    private readonly LookupContactAvailabilityHandler _availability;
    private readonly IVisitFrequencyPolicyResolver _frequency;

    public TerritoryReadinessReader(
        ITenantContext tenant,
        IAccountRepository accounts,
        IContactRepository contacts,
        IAccountContactLinkRepository links,
        IAccountTerritoryAssignmentRepository coverage,
        ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources,
        IContactAvailabilityRepository availability,
        IContactAvailabilityExceptionRepository exceptions,
        IVisitFrequencyPolicyResolver frequency)
    {
        _tenant = tenant;
        _accounts = accounts;
        _contacts = contacts;
        _links = links;
        _coverage = coverage;
        _models = models;
        _resources = resources;
        _availability = new LookupContactAvailabilityHandler(tenant, links, availability, exceptions, contacts, accounts);
        _frequency = frequency;
    }

    public Guid? TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<TerritoryRouteCandidateReadModel>> ForAccountAsync(
        Guid tenantId, Guid accountId, DateTimeOffset at, string? businessUnit, string? resourceId,
        Guid? contactId, DateOnly availabilityDate, bool requireResource, bool includeFrequencyBoundary,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(tenantId, accountId, cancellationToken);
        if (account is null) return [];

        var assignments = await _coverage.ListByAccountAsync(tenantId, accountId, cancellationToken);
        if (assignments.Count == 0)
        {
            return [await BuildAsync(tenantId, account, null, null, at, businessUnit, resourceId, contactId,
                availabilityDate, requireResource, includeFrequencyBoundary, cancellationToken)];
        }

        var rows = new List<TerritoryRouteCandidateReadModel>();
        foreach (var assignment in assignments)
        {
            var model = await _models.GetByIdAsync(tenantId, assignment.TerritoryModelId, cancellationToken);
            rows.Add(await BuildAsync(tenantId, account, assignment, model, at, businessUnit, resourceId, contactId,
                availabilityDate, requireResource, includeFrequencyBoundary, cancellationToken));
        }
        return rows;
    }

    public async Task<IReadOnlyList<TerritoryRouteCandidateReadModel>> ForContactAsync(
        Guid tenantId, Guid contactId, DateTimeOffset at, string? businessUnit, DateOnly availabilityDate,
        bool includeFrequencyBoundary, CancellationToken cancellationToken)
    {
        var contact = await _contacts.GetByIdAsync(tenantId, contactId, cancellationToken);
        if (contact is null) return [];

        var links = await _links.ListByContactAsync(tenantId, contactId, cancellationToken);
        if (links.Count == 0) return [];

        var rows = new List<TerritoryRouteCandidateReadModel>();
        foreach (var link in links)
        {
            rows.AddRange(await ForAccountAsync(tenantId, link.AccountId, at, businessUnit, null, contactId,
                availabilityDate, requireResource: false, includeFrequencyBoundary, cancellationToken));
        }
        return rows;
    }

    public async Task<IReadOnlyList<TerritoryRouteCandidateReadModel>> ForNodeAsync(
        Guid tenantId, Guid nodeId, DateTimeOffset at, string? businessUnit, bool requireResource,
        bool includeFrequencyBoundary, CancellationToken cancellationToken)
    {
        var models = await ListModelsAsync(tenantId, null, cancellationToken);
        var rows = new List<TerritoryRouteCandidateReadModel>();
        foreach (var model in models)
        {
            var assignments = (await _coverage.ListByModelAsync(tenantId, model.Id, cancellationToken))
                .Where(a => a.TerritoryNodeId == nodeId);
            foreach (var assignment in assignments)
            {
                var accountRows = await ForAccountAsync(tenantId, assignment.AccountId, at, businessUnit, null, null,
                    DateOnly.FromDateTime(at.UtcDateTime), requireResource, includeFrequencyBoundary, cancellationToken);
                rows.AddRange(accountRows.Where(r => r.TerritoryModelId == model.Id && r.TerritoryNodeId == nodeId));
            }
        }
        return rows;
    }

    public async Task<IReadOnlyList<TerritoryRouteCandidateReadModel>> ForResourceAsync(
        Guid tenantId, string resourceId, DateTimeOffset at, string? businessUnit, bool includeFrequencyBoundary,
        CancellationToken cancellationToken)
    {
        var rows = new List<TerritoryRouteCandidateReadModel>();
        var seen = new HashSet<(Guid AccountId, Guid AssignmentId)>();
        foreach (var responsibility in await _resources.ListByResourceAsync(tenantId, resourceId.Trim(), cancellationToken))
        {
            foreach (var assignment in await _coverage.ListByModelAsync(tenantId, responsibility.ModelId, cancellationToken))
            {
                if (responsibility.TerritoryId is not null && responsibility.TerritoryId != assignment.TerritoryNodeId) continue;
                if (!seen.Add((assignment.AccountId, assignment.Id))) continue;
                var accountRows = await ForAccountAsync(tenantId, assignment.AccountId, at, businessUnit, resourceId, null,
                    DateOnly.FromDateTime(at.UtcDateTime), requireResource: true, includeFrequencyBoundary, cancellationToken);
                rows.AddRange(accountRows.Where(r => r.TerritoryModelId == responsibility.ModelId));
            }
        }
        return rows;
    }

    public async Task<IReadOnlyList<TerritoryRouteCandidateReadModel>> RouteCandidatesAsync(
        Guid tenantId, GetRouteCandidatesQuery query, DateTimeOffset at, DateOnly availabilityDate,
        CancellationToken cancellationToken)
    {
        if (query.AccountId is { } accountId)
            return await ForAccountAsync(tenantId, accountId, at, query.BusinessUnit, query.ResourceId, query.ContactId,
                availabilityDate, requireResource: true, includeFrequencyBoundary: true, cancellationToken);
        if (query.ContactId is { } contactId)
            return await ForContactAsync(tenantId, contactId, at, query.BusinessUnit, availabilityDate,
                includeFrequencyBoundary: true, cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.ResourceId))
            return await ForResourceAsync(tenantId, query.ResourceId, at, query.BusinessUnit,
                includeFrequencyBoundary: true, cancellationToken);
        if (query.TerritoryNodeId is { } nodeId)
            return await ForNodeAsync(tenantId, nodeId, at, query.BusinessUnit, requireResource: true,
                includeFrequencyBoundary: true, cancellationToken);

        var models = await ListModelsAsync(tenantId, query.TerritoryModelId, cancellationToken);
        var rows = new List<TerritoryRouteCandidateReadModel>();
        foreach (var model in models)
        {
            foreach (var assignment in await _coverage.ListByModelAsync(tenantId, model.Id, cancellationToken))
            {
                var accountRows = await ForAccountAsync(tenantId, assignment.AccountId, at, query.BusinessUnit, null, null,
                    availabilityDate, requireResource: true, includeFrequencyBoundary: true, cancellationToken);
                rows.AddRange(accountRows.Where(r => r.TerritoryModelId == model.Id));
            }
        }
        return rows.DistinctBy(r => new { r.AccountId, r.TerritoryModelId, r.TerritoryNodeId, r.ContactId }).ToList();
    }

    private async Task<IReadOnlyList<TerritoryModel>> ListModelsAsync(
        Guid tenantId, Guid? modelId, CancellationToken cancellationToken)
    {
        if (modelId is { } id)
        {
            var model = await _models.GetByIdAsync(tenantId, id, cancellationToken);
            return model is null ? [] : [model];
        }
        var (items, _) = await _models.ListAsync(tenantId, null, null, 1, 10_000, cancellationToken);
        return items;
    }

    private async Task<TerritoryRouteCandidateReadModel> BuildAsync(
        Guid tenantId, AccountEntity account, AccountTerritoryAssignment? assignment, TerritoryModel? model,
        DateTimeOffset at, string? requestedBusinessUnit, string? requestedResourceId, Guid? contactId,
        DateOnly availabilityDate, bool requireResource, bool includeFrequencyBoundary, CancellationToken cancellationToken)
    {
        var blocking = new List<string>();
        var unknown = new List<string>();
        var warnings = new List<string>();

        var coverageCurrent = assignment is not null && model is not null
            && TerritoryCoverageLifecyclePolicy.IsCurrent(assignment, new Dictionary<Guid, TerritoryModel> { [model.Id] = model }, at);
        if (!coverageCurrent) blocking.Add(TerritoryReadinessReasonCodes.CoverageNotCurrent);
        if (!string.Equals(account.Status, "active", StringComparison.OrdinalIgnoreCase))
            blocking.Add(TerritoryReadinessReasonCodes.AccountInactive);

        var hasLocation = account.Latitude is not null && account.Longitude is not null
                          || !string.IsNullOrWhiteSpace(account.AddressLine)
                          || !string.IsNullOrWhiteSpace(account.CityRef)
                          || !string.IsNullOrWhiteSpace(account.DistrictRef);
        if (!hasLocation) blocking.Add(TerritoryReadinessReasonCodes.AccountMissingLocation);

        var scopes = assignment?.BusinessScopes ?? [];
        var businessUnit = string.IsNullOrWhiteSpace(requestedBusinessUnit)
            ? scopes.FirstOrDefault(s => string.Equals(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))?.ScopeCode
            : requestedBusinessUnit.Trim();
        if (!string.IsNullOrWhiteSpace(requestedBusinessUnit)
            && !scopes.Any(s => string.Equals(s.ScopeCode, requestedBusinessUnit.Trim(), StringComparison.OrdinalIgnoreCase)))
            blocking.Add(TerritoryReadinessReasonCodes.BusinessScopeMismatch);

        TerritoryResourceAssignment? owner = null;
        if (assignment is not null && model is not null)
        {
            owner = (await _resources.ListByModelAsync(tenantId, model.Id, cancellationToken))
                .Where(r => TerritoryCurrentResponsibilityPolicy.IsCurrent(r, at)
                            && (r.TerritoryId is null || r.TerritoryId == assignment.TerritoryNodeId)
                            && (string.IsNullOrWhiteSpace(businessUnit)
                                || r.BusinessScopes.Count == 0
                                || r.BusinessScopes.Any(s => string.Equals(s.ScopeCode, businessUnit, StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(r => r.IsPrimary)
                .FirstOrDefault(r => string.IsNullOrWhiteSpace(requestedResourceId)
                                     || string.Equals(r.Resource.ResourceId, requestedResourceId.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        if (requireResource && owner is null) blocking.Add(TerritoryReadinessReasonCodes.ResourceNotCurrentOwner);

        ContactEntity? contact = null;
        AccountContactLink? link = null;
        ContactAvailabilityLookupRowDto? availability = null;
        if (contactId is { } c)
        {
            contact = await _contacts.GetByIdAsync(tenantId, c, cancellationToken);
            link = (await _links.ListByContactAsync(tenantId, c, cancellationToken)).FirstOrDefault(l => l.AccountId == account.Id);
            if (link is null || !ContactAvailabilityValidation.IsLinkOpen(link))
                blocking.Add(TerritoryReadinessReasonCodes.ContactNotLinkedToAccount);
            if (contact is null || !string.Equals(contact.Status, "active", StringComparison.OrdinalIgnoreCase))
                blocking.Add(TerritoryReadinessReasonCodes.ContactInactive);

            if (link is not null)
            {
                var lookup = await _availability.Handle(new LookupContactAvailabilityQuery(
                    availabilityDate.ToString("yyyy-MM-dd"), c, account.Id, link.Id), cancellationToken);
                availability = lookup.Data?.Rows.FirstOrDefault(r => r.AccountContactLinkId == link.Id);
                if (availability is null || availability.AvailabilityStatus == AvailabilityLookupStatus.Unknown)
                    unknown.Add(TerritoryReadinessReasonCodes.ContactAvailabilityUnknown);
                else if (availability.AvailabilityStatus == AvailabilityLookupStatus.Unavailable)
                    blocking.Add(TerritoryReadinessReasonCodes.ContactNotAvailableOnDay);

                if (availability?.AppointmentRequired == true)
                    warnings.Add(TerritoryReadinessReasonCodes.AppointmentRequired);
                if (availability?.PreferredWindow is { } preferred && !IsInsideWindow(at, preferred))
                    warnings.Add(TerritoryReadinessReasonCodes.OutsidePreferredWindow);
            }
        }

        // MOD-0151 FU09B — read-only frequency provider consumption. ONLY the route-candidate path resolves frequency;
        // the coverage/contact/node/resource readiness paths keep it "not_requested" (unchanged FU09A semantics). The
        // MOD-0165 resolve engine is the single source of truth: FU09A supplies the most-specific available target
        // (link > contact > account) plus the known territory-node/business-unit context and NEVER re-decides the
        // selection. A default frequency is never invented; a provider failure degrades to "unknown" (never a silent
        // readiness_ok, never a 500). DueStatus stays unknown and LastVisitDate stays null.
        var frequencyStatus = "not_requested";
        VisitFrequencyResolveResult? frequency = null;
        if (includeFrequencyBoundary)
        {
            var (targetType, targetId) = link is not null
                ? (FrequencyTargetType.AccountContactLink, link.Id)
                : contact is not null
                    ? (FrequencyTargetType.Contact, contact.Id)
                    : (FrequencyTargetType.Account, account.Id);
            try
            {
                frequency = await _frequency.ResolveAsync(
                    new ResolveVisitFrequencyPolicyQuery(
                        targetType, targetId, at,
                        BusinessUnit: businessUnit,
                        TerritoryNodeId: assignment?.TerritoryNodeId,
                        IncludeDiagnostics: true),
                    cancellationToken);
            }
            catch
            {
                frequency = null; // controlled degrade to unknown below; never 500, never silent readiness_ok
            }

            frequencyStatus = frequency?.FrequencyStatus ?? FrequencyStatus.Unknown;
            if (frequencyStatus == FrequencyStatus.Conflict)
            {
                // Deterministically resolved to a policy, but the same-band tie is surfaced (never silent).
                warnings.Add(TerritoryReadinessReasonCodes.FrequencyConflict);
            }
            else if (frequencyStatus != FrequencyStatus.Resolved)
            {
                // unknown / not_applicable / provider error → readiness stays "unknown" (unchanged FU09A behaviour).
                unknown.Add(TerritoryReadinessReasonCodes.FrequencyUnknown);
            }
        }

        var status = blocking.Count > 0 ? TerritoryReadinessStatus.NotReady
            : unknown.Count > 0 ? TerritoryReadinessStatus.Unknown
            : TerritoryReadinessStatus.Ready;
        IReadOnlyList<string> reasons = blocking.Concat(unknown).Concat(warnings).Distinct().ToList();
        if (reasons.Count == 0) reasons = [TerritoryReadinessReasonCodes.ReadinessOk];

        return new TerritoryRouteCandidateReadModel(
            account.Id, account.AccountName, account.Status, hasLocation ? "ready" : "missing",
            account.Latitude, account.Longitude, BuildAddress(account), assignment?.TerritoryModelId, model?.Name,
            assignment?.TerritoryNodeId, assignment?.TerritoryNodeCode, assignment?.TerritoryNodeName, businessUnit,
            owner?.Resource.ResourceId, owner?.Resource.DisplayName, owner?.EffectivePositionCode, owner?.EffectivePositionTitle,
            contact?.Id ?? contactId, contact?.DisplayName, link?.Id,
            availability?.AvailabilityStatus ?? (contactId is null ? "not_requested" : AvailabilityLookupStatus.Unknown),
            availability?.AvailableWindow, availability?.PreferredWindow, availability?.AvoidWindow,
            availability?.AppointmentRequired ?? false, availability?.AverageVisitDurationMinutes,
            frequencyStatus,
            frequency?.SelectedFrequencyPolicyId,
            frequency?.SelectedPolicyCode,
            frequency?.SelectedPolicyName,
            frequency?.FrequencyType,
            frequency?.RequiredVisitCount,
            frequency?.PeriodType,
            frequency?.SelectionReason,
            frequency?.ReasonCodes ?? [],
            frequency?.CandidatePolicies ?? [],
            // DueStatus stays unknown and LastVisitDate stays null — FU09B does NOT compute due/overdue or last visit.
            null,
            includeFrequencyBoundary ? "unknown" : "not_requested",
            at, status, reasons);
    }

    private static string? BuildAddress(AccountEntity account)
    {
        var parts = new[] { account.AddressLine, account.DistrictRef, account.CityRef, account.CountryRef }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var address = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    private static bool IsInsideWindow(DateTimeOffset at, string window)
    {
        var parts = window.Split('-', StringSplitOptions.TrimEntries);
        return parts.Length != 2
               || !TimeOnly.TryParse(parts[0], out var start)
               || !TimeOnly.TryParse(parts[1], out var end)
               || (TimeOnly.FromDateTime(at.UtcDateTime) >= start && TimeOnly.FromDateTime(at.UtcDateTime) <= end);
    }
}

internal abstract class TerritoryReadinessHandlerBase
{
    protected readonly TerritoryReadinessReader Reader;
    protected TerritoryReadinessHandlerBase(TerritoryReadinessReader reader) => Reader = reader;

    internal static Response<TerritoryReadinessResultDto> Result(
        IReadOnlyList<TerritoryRouteCandidateReadModel> all, bool includeNonReady)
    {
        var items = includeNonReady ? all : all.Where(x => x.ReadinessStatus == TerritoryReadinessStatus.Ready).ToList();
        return Response<TerritoryReadinessResultDto>.Success(new TerritoryReadinessResultDto(
            all.Count,
            all.Count(x => x.ReadinessStatus == TerritoryReadinessStatus.Ready),
            all.Count(x => x.ReadinessStatus == TerritoryReadinessStatus.NotReady),
            all.Count(x => x.ReadinessStatus == TerritoryReadinessStatus.Unknown),
            items.Count,
            items));
    }

    internal static bool TryDate(DateTimeOffset at, string? date, string? weekday, out DateOnly value, out string? error)
    {
        value = DateOnly.FromDateTime(at.UtcDateTime);
        error = null;
        if (!string.IsNullOrWhiteSpace(date) && !DateOnly.TryParseExact(date, "yyyy-MM-dd", out value))
        {
            error = "date must be a calendar date in yyyy-MM-dd format.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(weekday) && !AvailabilityWeekday.IsValid(weekday))
        {
            error = "weekday must be monday, tuesday, wednesday, thursday, friday, saturday or sunday.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(weekday)
            && !string.Equals(AvailabilityWeekday.FromDate(value), weekday.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            error = "weekday does not match the requested date/effectiveAt calendar day.";
            return false;
        }
        return true;
    }
}

public sealed class GetAccountCoverageReadinessHandler : IRequestHandler<GetAccountCoverageReadinessQuery, Response<TerritoryReadinessResultDto>>
{
    private readonly TerritoryReadinessReader _reader;
    public GetAccountCoverageReadinessHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IAccountTerritoryAssignmentRepository coverage, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources, IContactAvailabilityRepository availability,
        IContactAvailabilityExceptionRepository exceptions, IVisitFrequencyPolicyResolver frequency)
        => _reader = new(tenant, accounts, contacts, links, coverage, models, resources, availability, exceptions, frequency);

    public async Task<Response<TerritoryReadinessResultDto>> Handle(GetAccountCoverageReadinessQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return Response<TerritoryReadinessResultDto>.Fail("Tenant context is required.", 400);
        var rows = await _reader.ForAccountAsync(tenantId, request.AccountId, request.EffectiveAt ?? DateTimeOffset.UtcNow,
            request.BusinessUnit, null, null, DateOnly.FromDateTime((request.EffectiveAt ?? DateTimeOffset.UtcNow).UtcDateTime), false, false, ct);
        return rows.Count == 0 ? Response<TerritoryReadinessResultDto>.Fail("Account not found.", 404)
            : TerritoryReadinessHandlerBase.Result(rows, true);
    }
}

public sealed class GetNodeCoverageAccountsHandler : IRequestHandler<GetNodeCoverageAccountsQuery, Response<TerritoryReadinessResultDto>>
{
    private readonly TerritoryReadinessReader _reader;
    public GetNodeCoverageAccountsHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IAccountTerritoryAssignmentRepository coverage, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources, IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IVisitFrequencyPolicyResolver frequency)
        => _reader = new(tenant, accounts, contacts, links, coverage, models, resources, availability, exceptions, frequency);
    public async Task<Response<TerritoryReadinessResultDto>> Handle(GetNodeCoverageAccountsQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return Response<TerritoryReadinessResultDto>.Fail("Tenant context is required.", 400);
        var rows = await _reader.ForNodeAsync(tenantId, request.NodeId, request.EffectiveAt ?? DateTimeOffset.UtcNow,
            request.BusinessUnit, false, false, ct);
        return TerritoryReadinessHandlerBase.Result(rows, request.IncludeNonReady);
    }
}

public sealed class GetResourceCoverageReadinessHandler : IRequestHandler<GetResourceCoverageReadinessQuery, Response<TerritoryReadinessResultDto>>
{
    private readonly TerritoryReadinessReader _reader;
    public GetResourceCoverageReadinessHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IAccountTerritoryAssignmentRepository coverage, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources, IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IVisitFrequencyPolicyResolver frequency)
        => _reader = new(tenant, accounts, contacts, links, coverage, models, resources, availability, exceptions, frequency);
    public async Task<Response<TerritoryReadinessResultDto>> Handle(GetResourceCoverageReadinessQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return Response<TerritoryReadinessResultDto>.Fail("Tenant context is required.", 400);
        if (string.IsNullOrWhiteSpace(request.ResourceId)) return Response<TerritoryReadinessResultDto>.Fail("ResourceId is required.", 400);
        var rows = await _reader.ForResourceAsync(tenantId, request.ResourceId, request.EffectiveAt ?? DateTimeOffset.UtcNow,
            request.BusinessUnit, false, ct);
        return TerritoryReadinessHandlerBase.Result(rows, request.IncludeNonReady);
    }
}

public sealed class GetContactTerritoryCoverageHandler : IRequestHandler<GetContactTerritoryCoverageQuery, Response<TerritoryReadinessResultDto>>
{
    private readonly TerritoryReadinessReader _reader;
    public GetContactTerritoryCoverageHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IAccountTerritoryAssignmentRepository coverage, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources, IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IVisitFrequencyPolicyResolver frequency)
        => _reader = new(tenant, accounts, contacts, links, coverage, models, resources, availability, exceptions, frequency);
    public async Task<Response<TerritoryReadinessResultDto>> Handle(GetContactTerritoryCoverageQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return Response<TerritoryReadinessResultDto>.Fail("Tenant context is required.", 400);
        var at = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        if (!TerritoryReadinessHandlerBase.TryDate(at, request.Date, request.Weekday, out var date, out var error))
            return Response<TerritoryReadinessResultDto>.Fail(error!, 400);
        var rows = await _reader.ForContactAsync(tenantId, request.ContactId, at, request.BusinessUnit, date, false, ct);
        return rows.Count == 0 ? Response<TerritoryReadinessResultDto>.Fail("Contact or active account link not found.", 404)
            : TerritoryReadinessHandlerBase.Result(rows, true);
    }
}

public sealed class GetRouteCandidatesHandler : IRequestHandler<GetRouteCandidatesQuery, Response<TerritoryReadinessResultDto>>
{
    private readonly TerritoryReadinessReader _reader;
    public GetRouteCandidatesHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IAccountTerritoryAssignmentRepository coverage, ITerritoryModelRepository models,
        ITerritoryResourceAssignmentRepository resources, IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IVisitFrequencyPolicyResolver frequency)
        => _reader = new(tenant, accounts, contacts, links, coverage, models, resources, availability, exceptions, frequency);
    public async Task<Response<TerritoryReadinessResultDto>> Handle(GetRouteCandidatesQuery request, CancellationToken ct)
    {
        if (_reader.TenantId is not { } tenantId) return Response<TerritoryReadinessResultDto>.Fail("Tenant context is required.", 400);
        var at = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        if (!TerritoryReadinessHandlerBase.TryDate(at, request.Date, request.Weekday, out var date, out var error))
            return Response<TerritoryReadinessResultDto>.Fail(error!, 400);
        var rows = await _reader.RouteCandidatesAsync(tenantId, request, at, date, ct);
        return TerritoryReadinessHandlerBase.Result(rows, request.IncludeNonReady);
    }
}
