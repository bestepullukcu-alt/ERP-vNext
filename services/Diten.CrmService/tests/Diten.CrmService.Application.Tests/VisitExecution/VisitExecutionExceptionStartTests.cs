using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3 · W3-BE-c (Acceptance "Başlatma") on the PRODUCTION start handler + resolver: an APPROVED exception ACTIVE
/// today relaxes the tenant rules (a far start under <c>location_info</c> in a location-required tenant is accepted,
/// recorded with <c>started_with_exception</c> + <c>exceptionId</c> + <c>relaxedRules</c>); an in-review, planned,
/// expired, cancelled or another rep's exception relaxes nothing; the contract shows the caller's effective rules.
/// The clock is the W3 fixture's (2026-10-15 UTC).
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private readonly ExceptionStore _exceptionStore = new();

    private StartVisitExecutionHandler ExceptionAwareStarter()
        => new(TenantCtx(), new Application.Common.NullActorContext(), RepCaller(), _plans, _reports, _executions, _policies,
            _accounts, _store, _clock, new VisitEvidencePolicyResolver(_policies, _exceptionStore));

    private VisitEvidenceException Exception(string status, DateOnly start, DateOnly end, string rep = Rep, params string[] rules)
    {
        var e = new VisitEvidenceException
        {
            TenantId = Tenant, ResourceId = rep, Status = status, StartDate = start, EndDate = end, Reason = "r",
            Rules = (rules.Length == 0 ? new[] { VisitEvidenceExceptionRules.LocationInfo } : rules).ToList()
        };
        _exceptionStore.Items.Add(e);
        return e;
    }

    [Fact]
    public async Task An_active_approved_exception_relaxes_the_location_rule_and_marks_the_start()
    {
        // the tenant: location required (the default) — a far start is refused without an exception
        AssertCode(await ExceptionAwareStarter().Handle(StartAt(Seed(), lat: FarLat), default), 409, VisitExecutionErrorCodes.LocationTooFar);

        var exception = Exception(VisitEvidenceExceptionStatuses.Approved, Today.AddDays(-1), Today.AddDays(2));
        var started = await ExceptionAwareStarter().Handle(StartAt(Seed(), lat: FarLat), default);

        Assert.Equal(201, started.StatusCode);
        var dto = started.Data!;
        Assert.Contains(VisitExecutionFlags.StartedWithException, dto.Flags);
        Assert.Contains(VisitExecutionFlags.LocationFar, dto.Flags);                  // still recorded and flagged
        Assert.Equal(exception.Id, dto.ExceptionId);
        Assert.Equal(new[] { VisitEvidenceExceptionRules.LocationInfo }, dto.RelaxedRules);
        Assert.Equal(VisitLocationModes.Info, dto.Location.Mode);
        Assert.Equal(exception.Id, _executions.Items.Last().ExceptionId);
        Assert.Null(_policies.Stored);                                                 // the tenant policy never changes
    }

    [Theory]
    [InlineData(VisitEvidenceExceptionStatuses.InReview, -1, 2, Rep)]
    [InlineData(VisitEvidenceExceptionStatuses.Draft, -1, 2, Rep)]
    [InlineData(VisitEvidenceExceptionStatuses.Cancelled, -1, 2, Rep)]
    [InlineData(VisitEvidenceExceptionStatuses.EndedEarly, -3, -1, Rep)]
    [InlineData(VisitEvidenceExceptionStatuses.Approved, -5, -1, Rep)]  // expired yesterday
    [InlineData(VisitEvidenceExceptionStatuses.Approved, 1, 3, Rep)]    // planned (starts tomorrow)
    [InlineData(VisitEvidenceExceptionStatuses.Approved, -1, 2, OtherRep)]
    public async Task An_unapproved_expired_planned_or_foreign_exception_relaxes_nothing(string status, int from, int to, string rep)
    {
        Exception(status, Today.AddDays(from), Today.AddDays(to), rep);
        var res = await ExceptionAwareStarter().Handle(StartAt(Seed(), lat: FarLat), default);
        AssertCode(res, 409, VisitExecutionErrorCodes.LocationTooFar);
        Assert.Empty(_executions.Items);
    }

    [Fact]
    public async Task A_photo_optional_exception_starts_without_the_photo_a_required_tenant_asks_for_and_the_contract_shows_it()
    {
        _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, PhotoMode = VisitPhotoModes.Required, Version = 1 };
        AssertCode(await ExceptionAwareStarter().Handle(StartAt(Seed()), default), 400, VisitExecutionErrorCodes.PhotoRequired);

        Exception(VisitEvidenceExceptionStatuses.Approved, Today, Today, Rep, VisitEvidenceExceptionRules.PhotoOptional);
        var res = await ExceptionAwareStarter().Handle(StartAt(Seed()), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Equal(new[] { VisitEvidenceExceptionRules.PhotoOptional }, res.Data!.RelaxedRules);

        var contract = (await new GetVisitExecutionContractHandler(TenantCtx(), _policies, RepCaller(), new VisitEvidencePolicyResolver(_policies, _exceptionStore), _clock)
            .Handle(new GetVisitExecutionContractQuery(), default)).Data!;
        Assert.Equal(VisitPhotoModes.Optional, contract.Policy.PhotoMode);              // the rep's effective rules
        Assert.Contains(VisitExecutionFlags.StartedWithException, contract.FlagCodes);
        var tenantView = (await new GetVisitExecutionContractHandler(TenantCtx(), _policies, new TestCallerScope(null), new VisitEvidencePolicyResolver(_policies, _exceptionStore), _clock)
            .Handle(new GetVisitExecutionContractQuery(), default)).Data!;
        Assert.Equal(VisitPhotoModes.Required, tenantView.Policy.PhotoMode);            // no resource id → the tenant's
    }

    private sealed class ExceptionStore : IVisitEvidenceExceptionRepository
    {
        public List<VisitEvidenceException> Items { get; } = new();
        public Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id));
        public Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.ToList());
        public Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.Where(e => e.TenantId == tenantId && e.ResourceId == resourceId).ToList());
        public Task InsertAsync(VisitEvidenceException entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }
}
