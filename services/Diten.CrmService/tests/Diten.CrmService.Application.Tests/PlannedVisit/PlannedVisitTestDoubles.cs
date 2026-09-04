using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 test doubles — pure in-memory, no Mongo. Every consumed seam (frequency resolver, consent evaluator,
/// journey reader, contact-availability + target repos) is a fake, so the FU01 rules are exercised without a database
/// and without touching another module.
/// </summary>
internal sealed class FakePlannedVisitRepository : IPlannedVisitRepository
{
    public List<PlannedVisitEntity> Items { get; } = new();
    public int InsertCount { get; private set; }
    public int ReplaceCount { get; private set; }

    private IEnumerable<PlannedVisitEntity> Scope(Guid tenantId)
        => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public Task<PlannedVisitEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Scope(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<PlannedVisitEntity>> ListAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Scope(tenantId).ToList());

    public Task<IReadOnlyList<PlannedVisitEntity>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(
            Scope(tenantId).Where(x => x.VisitCode == visitCode).ToList());

    public Task<IReadOnlyList<PlannedVisitEntity>> ListByResourceAndDateAsync(
        Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(
            Scope(tenantId).Where(x => x.Resource.ResourceId == resourceId && x.PlannedDate == plannedDate).ToList());

    public Task<IReadOnlyList<PlannedVisitEntity>> ListByTargetAndDateAsync(
        Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(
            Scope(tenantId).Where(x => x.TargetId == targetId && x.PlannedDate == plannedDate).ToList());

    public Task InsertAsync(PlannedVisitEntity entity, CancellationToken ct)
    {
        InsertCount++;
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(PlannedVisitEntity entity, int expectedVersion, CancellationToken ct)
    {
        ReplaceCount++;
        var existing = Items.FirstOrDefault(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
        if (existing is null || existing.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Items[Items.IndexOf(existing)] = entity;
        return Task.FromResult(true);
    }
}

internal sealed class FakeAccountRepository : IAccountRepository
{
    public List<Account> Items { get; } = new();

    public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted));

    public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
        IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task InsertAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
}

internal sealed class FakeContactRepository : IContactRepository
{
    public List<Contact> Items { get; } = new();

    public Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(
            Items.Where(c => c.TenantId == tenantId && !c.IsDeleted && ids.Contains(c.Id)).ToList());

    public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task InsertAsync(Contact contact, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateAsync(Contact contact, CancellationToken ct) => throw new NotImplementedException();
}

internal sealed class FakeLinkRepository : IAccountContactLinkRepository
{
    public List<AccountContactLink> Items { get; } = new();

    public Task<AccountContactLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == tenantId && l.Id == id && !l.IsDeleted));

    public Task<bool> ExistsActiveAsync(
        Guid tenantId, Guid accountId, Guid contactId, string roleCode, Guid? excludeId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<bool> ExistsPrimaryAsync(
        Guid tenantId, Guid accountId, string roleCode, Guid? excludeId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid tenantId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task InsertAsync(AccountContactLink link, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateAsync(AccountContactLink link, CancellationToken ct) => throw new NotImplementedException();
}

internal sealed class FakeCampaignRepository : ICampaignRepository
{
    public List<Campaign> Items { get; } = new();

    public Task<Campaign?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Campaign>> ListAsync(Guid tenantId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<Campaign?> GetActiveByCodeAsync(Guid tenantId, string campaignCode, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<Campaign?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken ct)
        => throw new NotImplementedException();
    public Task InsertAsync(Campaign campaign, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateAsync(Campaign campaign, CancellationToken ct) => throw new NotImplementedException();
}

internal sealed class FakeAvailabilityRepository : IContactAvailabilityRepository
{
    public List<ContactAvailability> Items { get; } = new();

    public Task<ContactAvailability?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => throw new NotImplementedException();
    public Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ContactAvailability>>(
            Items.Where(a => a.TenantId == tenantId && a.AccountContactLinkId == linkId).ToList());
    public Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ContactAvailability>>(
            Items.Where(a => a.TenantId == tenantId && a.ContactId == contactId).ToList());
    public Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ContactAvailability>>(
            Items.Where(a => a.TenantId == tenantId && a.AccountId == accountId).ToList());
    public Task InsertAsync(ContactAvailability availability, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateAsync(ContactAvailability availability, CancellationToken ct) => throw new NotImplementedException();
}

/// <summary>Frequency resolver stub — returns a configurable result (default: unknown, never a fabricated default).</summary>
internal sealed class FakeFrequencyResolver : IVisitFrequencyPolicyResolver
{
    public VisitFrequencyResolveResult Result { get; set; } = Unknown();

    public Task<VisitFrequencyResolveResult> ResolveAsync(
        ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result);

    public static VisitFrequencyResolveResult Unknown() => new(
        FrequencyStatus.Unknown, null, null, null, null, null, null, null, null, null, null, null, null, null,
        Array.Empty<FrequencyCandidatePolicy>(), new[] { FrequencyReasonCodes.NoMatchingPolicy });

    public static VisitFrequencyResolveResult Resolved(Guid policyId) => new(
        FrequencyStatus.Resolved, policyId, "P1", "Policy One", "selected", 2, "count", "month", null, null,
        null, null, 1, "manual",
        Array.Empty<FrequencyCandidatePolicy>(), new[] { FrequencyReasonCodes.FrequencyPolicyResolved });
}

/// <summary>Consent evaluator stub — returns a configurable verdict (default: allowed).</summary>
internal sealed class FakeConsentEvaluator : IConsentPreferenceEvaluator
{
    public string Status { get; set; } = ConsentEligibilityStatus.Allowed;

    public Task<ConsentEvaluationResult> EvaluateAsync(ConsentEvaluationRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new ConsentEvaluationResult(
            Status,
            ConsentDecision.ConsentGranted,
            request.SubjectType,
            request.SubjectId,
            request.Channel,
            request.Purpose,
            null,
            null,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Array.Empty<Guid>(),
            new[] { ConsentReasonCodes.ConsentGranted },
            "selected",
            Array.Empty<CandidateConsent>(),
            Array.Empty<CandidatePreference>(),
            ConsentEvaluationResult.CurrentEvaluatorVersion,
            DateTimeOffset.UtcNow));
}

/// <summary>Journey reader stub — returns configurable published journeys (default: none, i.e. nothing is published).</summary>
internal sealed class FakeJourneyReader : IContentEngagementJourneyReader
{
    public List<ContentEngagementJourneyDto> Journeys { get; } = new();

    public Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
        ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ContentEngagementJourneyDto>>(Journeys);

    public Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
        Guid journeyId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ContentEngagementJourneyStageDto>>(
            Journeys.FirstOrDefault(j => j.JourneyId == journeyId)?.Stages ?? Array.Empty<ContentEngagementJourneyStageDto>());

    public static ContentEngagementJourneyStageDto Stage(Guid id, int order, string code, string name)
        => new(id, order, code, name, "obj", null, Guid.Empty, "", "latest", false, false, null, null, null, null,
            null, Array.Empty<ContentEngagementJourneyBranchConditionDto>(), "active", null, null, null, null,
            "resolved", false, false, 1, null, null, DateTimeOffset.UtcNow, null, null, null, false);

    public static ContentEngagementJourneyDto Journey(
        Guid id, string name, IReadOnlyList<ContentEngagementJourneyStageDto> stages)
    {
        var now = DateTimeOffset.UtcNow;
        return new(id, "J1", name, null, Guid.NewGuid(), null, null, "obj", null, "1", "published",
            now.AddDays(-1), null, "manual", stages, stages.Count, 0, 0, false, false, false, false, false, null,
            now, null, null, 1, now, null, null, null, null, null, false);
    }
}
