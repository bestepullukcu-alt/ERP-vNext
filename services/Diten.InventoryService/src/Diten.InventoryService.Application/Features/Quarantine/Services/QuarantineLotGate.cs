using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175 Q4 (KQ-I) — replaces <c>AlwaysAllowLotStatusChangeGate</c>. Only the manual RELEASE of a lot is gated: Draft → Released and
/// Blocked → Released answer <see cref="LotStatusGateDecision.PendingApproval"/> while the company's
/// <see cref="QuarantineSettings.LotReleaseRequiresApproval"/> is on (default on, CT-Q10). Released → Blocked (safety direction) and
/// every other change answer <see cref="LotStatusGateDecision.Allow"/>; the switch off answers Allow (today's behaviour).
/// </summary>
public sealed class QuarantineLotStatusChangeGate : ILotStatusChangeGate
{
    private readonly QuarantineFacts _facts;

    public QuarantineLotStatusChangeGate(QuarantineFacts facts) => _facts = facts;

    public static bool IsGated(LotStatus from, LotStatus target) => target == LotStatus.Released && from is LotStatus.Draft or LotStatus.Blocked;

    public async Task<LotStatusGateDecision> EvaluateAsync(Lot lot, LotStatus target, string reason, CancellationToken cancellationToken = default)
    {
        if (!IsGated(lot.Status, target))
        {
            return LotStatusGateDecision.Allow;
        }

        return (await _facts.SettingsAsync(cancellationToken)).LotReleaseRequiresApproval
            ? LotStatusGateDecision.PendingApproval
            : LotStatusGateDecision.Allow;
    }
}

/// <summary>
/// MOD-0175 Q4 (KQ-I): the approval side of the lot gate.
/// <list type="bullet">
/// <item><see cref="OpenAsync"/> — the gated request becomes a <c>QuarantineDecision{Kind: LotRelease}</c> DRAFT (requester = decider,
/// reason = the request's reason, <c>lotStatusTarget = Released</c>) in its own MOD-0175 transaction; the lot transaction wrote nothing.
/// The same request key answers the same decision; an open LotRelease decision of the lot (Draft / Submitted / StartFailed /
/// PendingApproval / Approved) is answered instead of a second one. The requester then signs and submits it (KQ-I step 3).</item>
/// <item><see cref="FindApprovedAsync"/> — what <see cref="ApplyApprovedLotStatusChangeCommand"/> may apply: only a decision of this
/// company whose state is Approved (or ApplyFailed — approved, apply to finish); LotRelease → Released from the status it was requested
/// from (Draft or Blocked: a lot blocked meanwhile is not released by an approval of its Draft release), a hold
/// release on a Draft lot (CT-Q5) → Released from Draft only, a retest release → its new retest date.</item>
/// </list>
/// </summary>
public sealed class QuarantineLotReleaseApprovals : ILotStatusChangeApprovals, IApprovedLotChanges
{
    private static readonly IReadOnlySet<LotStatus> ReleaseFrom = new HashSet<LotStatus> { LotStatus.Draft, LotStatus.Blocked };
    private static readonly IReadOnlySet<LotStatus> DraftOnly = new HashSet<LotStatus> { LotStatus.Draft };

    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineSupport _support;

    public QuarantineLotReleaseApprovals(IQuarantineRepository repository, IQuarantineDecisionRepository decisions, QuarantineSupport support)
    {
        _repository = repository;
        _decisions = decisions;
        _support = support;
    }

    /// <summary>The decision key of a gated request: user-scoped by the repository, lot + request key hashed (≤ 128).</summary>
    public static string DecisionKey(Guid lotId, string requestKey)
        => "lot-release:" + QuarantineSupport.Fingerprint(("lot", lotId.ToString("D")), ("key", requestKey))[..40].ToLowerInvariant();

    public async Task<string> OpenAsync(Lot lot, LotStatus target, string reason, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = DecisionKey(lot.Id, idempotencyKey);
        if (await _decisions.FindByIdempotencyKeyAsync(_support.UserId, key, cancellationToken) is { } replay)
        {
            return replay.Id.ToString("D");
        }

        if (await OpenReleaseAsync(lot.Id.ToString("D"), null, cancellationToken) is { } open
            && (open.LotStatusFrom is null || string.Equals(open.LotStatusFrom, lot.Status.ToString(), StringComparison.Ordinal)))
        {
            return open.Id.ToString("D");
        }

        var now = QuarantineSupport.Now();
        var decision = new QuarantineDecision
        {
            Id = Guid.NewGuid(),
            TenantId = _support.TenantId,
            LegalEntityId = _support.LegalEntityId,
            CreatedAt = now,
            Kind = DecisionKind.LotRelease,
            ItemId = lot.ItemId,
            SkuId = lot.SkuId,
            SkuLevel = lot.SkuLevel,
            LotId = lot.Id.ToString("D"),
            FromStockStatus = StockStatus.Available,
            Reason = reason,
            CoaReference = lot.CoaReference,
            LotStatusTarget = target.ToString(),
            LotStatusFrom = lot.Status.ToString(),
            DeciderUserId = _support.UserId,
            DecidedAtUtc = now,
            IdempotencyKey = key,
            RequestFingerprint = QuarantineSupport.Fingerprint(("kind", nameof(DecisionKind.LotRelease)), ("lot", lot.Id.ToString("D")),
                ("target", target.ToString()), ("reason", reason))
        };

        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                decision.DecisionNumber = await session.NextNumberAsync(QuarantineContract.DecisionPrefix, now.UtcDateTime.Year, c);
                await session.InsertDecisionAsync(decision, c);
                return true;
            }, cancellationToken);
        }
        catch (QuarantineDuplicateException)
        {
            var winner = await _decisions.FindByIdempotencyKeyAsync(_support.UserId, key, cancellationToken)
                         ?? throw new InvalidOperationException("Lot release key reported taken but no decision carries it.");
            return winner.Id.ToString("D");
        }

        return decision.Id.ToString("D");
    }

    /// <summary>An open LotRelease decision of the lot other than <paramref name="except"/> (V6 for a lot), or null.</summary>
    public async Task<QuarantineDecision?> OpenReleaseAsync(string lotId, Guid? except, CancellationToken ct)
    {
        var releases = (await _decisions.ListLotReleasesAsync(lotId, ct)).Where(d => d.Id != except).ToList();
        if (releases.Count == 0)
        {
            return null;
        }

        var events = (await _decisions.EventsAsync(releases.Select(d => d.Id).ToList(), ct)).ToLookup(e => e.DecisionId);
        return releases.OrderBy(d => d.CreatedAt)
            .FirstOrDefault(d => QuarantineFacts.StateOf(events[d.Id]) is DecisionState.Draft or DecisionState.Submitted or DecisionState.StartFailed
                or DecisionState.PendingApproval or DecisionState.Approved or DecisionState.ApplyFailed);
    }

    /// <summary>A LotRelease of the lot other than <paramref name="except"/> that is already sent (Submitted … ApplyFailed), or null.</summary>
    public async Task<QuarantineDecision?> WaitingReleaseAsync(string lotId, Guid except, CancellationToken ct)
    {
        var releases = (await _decisions.ListLotReleasesAsync(lotId, ct)).Where(d => d.Id != except).ToList();
        if (releases.Count == 0)
        {
            return null;
        }

        var events = (await _decisions.EventsAsync(releases.Select(d => d.Id).ToList(), ct)).ToLookup(e => e.DecisionId);
        return releases.FirstOrDefault(d => QuarantineFacts.StateOf(events[d.Id]) is DecisionState.Submitted or DecisionState.PendingApproval
            or DecisionState.Approved or DecisionState.ApplyFailed);
    }

    public async Task<ApprovedLotChange?> FindApprovedAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        var decision = await _decisions.FindAsync(decisionId, cancellationToken);
        if (decision is null || !Guid.TryParse(decision.LotId, out var lotId))
        {
            return null;
        }

        var events = (await _decisions.EventsAsync([decision.Id], cancellationToken)).OrderBy(e => e.Sequence).ToList();
        if (QuarantineFacts.StateOf(events) is not (DecisionState.Approved or DecisionState.ApplyFailed))
        {
            return null;
        }

        var approver = events.LastOrDefault(e => e.Kind == DecisionEventKind.Approved)?.ApproverUserId ?? Guid.Empty;
        var releases = decision.Lines.Any(l => l.Decision == LineDecision.Release);
        var target = string.Equals(decision.LotStatusTarget, nameof(LotStatus.Released), StringComparison.Ordinal) ? LotStatus.Released : (LotStatus?)null;
        var retest = releases ? decision.NewRetestDate : null;
        if (target is null && retest is null)
        {
            return null; // the decision changes nothing on the lot
        }

        return new ApprovedLotChange(decision.Id, decision.DecisionNumber, lotId, target,
            decision.Kind != DecisionKind.LotRelease ? DraftOnly
            : Enum.TryParse<LotStatus>(decision.LotStatusFrom, out var from) && ReleaseFrom.Contains(from) ? new HashSet<LotStatus> { from } : ReleaseFrom,
            retest, decision.Reason, approver);
    }
}
