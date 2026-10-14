using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitQr;

/// <summary>
/// The QR outcome of a visit start: the verdict to record (<see cref="Qr"/>), a flag to add, or a refusal. <see cref="MatchedCodeId"/>
/// is the code whose scan counter goes up after the start was written (only on <c>ok</c>).
/// <para>WP-VW-W3E (W3E-BE-c, §2.10) — <see cref="ExpectedHasActiveCode"/>: the visit's institution chain has an ACTIVE code
/// (read once the key ring is available; false for off / remote / no key). The pre-check tells <c>not_issued</c> from
/// <c>missing</c> with it before anything is scanned.</para>
/// </summary>
public sealed record VisitQrEvaluation(
    VisitExecutionQr Qr, string? Flag, VisitExecutionRules.Failure? Refusal, Guid? MatchedCodeId, bool ExpectedHasActiveCode = false);

/// <summary>WP-VW-W3E (W3E-BE-a, §2.4) — the ONE call the start handler makes for the QR code.</summary>
public interface IVisitQrVerifier
{
    Task<VisitQrEvaluation> EvaluateAsync(
        Guid tenantId, PlanAtom plan, string kind, string qrMode, string? payload, DateTimeOffset? scannedAt, CancellationToken cancellationToken);

    /// <summary>After the start was written: the matched code's scan counter (atomic <c>$inc</c>; never blocks, a failure is logged).</summary>
    Task RecordScanAsync(Guid tenantId, VisitQrEvaluation evaluation, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.4) — the QR rule of a visit start. The expected institution is the location rule's
/// (<see cref="VisitExecutionRules.AccountOf"/>) or one of its parents (<c>ParentAccountId</c>, at most
/// <see cref="VisitQrLimits.MaxParentDepth"/> levels, cycle-guarded — K-W3E-5). The verdict:
/// <list type="bullet">
/// <item><c>off</c> or a remote visit → <c>not_applicable</c> (a payload is ignored).</item>
/// <item>no signing key → <c>unverifiable</c> (required: 503; optional: a flag only when a payload was sent).</item>
/// <item>no payload → <c>missing</c>, or <c>not_issued</c> when no institution of the chain has an active code.</item>
/// <item>bad shape / signature / unknown key / another tenant → <c>invalid</c>.</item>
/// <item>a valid code of an institution outside the chain → <c>wrong_account</c>.</item>
/// <item>a valid code of the chain that is not its institution's ACTIVE version → <c>revoked</c>.</item>
/// <item>else <c>ok</c> (matched via self | parent).</item>
/// </list>
/// Then the mode decides: required refuses missing / invalid / wrong_account / revoked / unverifiable and starts
/// not_issued with a flag (K-W3E-6); optional starts every verdict, flagging only invalid / wrong_account / revoked /
/// unverifiable (K-W3E-7). The payload is never logged.
/// </summary>
public sealed class VisitQrVerifier : IVisitQrVerifier
{
    private readonly IInstitutionQrCodeRepository _codes;
    private readonly IAccountRepository _accounts;
    private readonly IVisitQrKeyRing _keys;
    private readonly ILogger<VisitQrVerifier> _logger;

    public VisitQrVerifier(IInstitutionQrCodeRepository codes, IAccountRepository accounts, IVisitQrKeyRing keys, ILogger<VisitQrVerifier>? logger = null)
    {
        _codes = codes;
        _accounts = accounts;
        _keys = keys;
        _logger = logger ?? NullLogger<VisitQrVerifier>.Instance;
    }

    public async Task<VisitQrEvaluation> EvaluateAsync(
        Guid tenantId, PlanAtom plan, string kind, string qrMode, string? payload, DateTimeOffset? scannedAt, CancellationToken cancellationToken)
    {
        var mode = VisitExecutionRules.Normalize(qrMode) ?? VisitQrModes.Off;
        var text = string.IsNullOrWhiteSpace(payload) ? null : payload.Trim();
        if (mode == VisitQrModes.Off || string.Equals(kind, VisitExecutionKinds.Remote, StringComparison.Ordinal))
        {
            return Pass(VisitQrScanStatuses.NotApplicable, scannedAt: null);
        }

        var required = mode == VisitQrModes.Required; // an over-long text (> 200) fails the shape → invalid

        var keys = await _keys.GetAsync(cancellationToken);
        if (!keys.Available)
        {
            if (required)
            {
                return Refuse(VisitQrScanStatuses.Unverifiable, scannedAt, new VisitExecutionRules.Failure(
                    "The QR code cannot be verified right now. Please try again later.", VisitExecutionErrorCodes.QrSigningUnavailable, 503));
            }

            return text is null
                ? Pass(VisitQrScanStatuses.Missing, scannedAt)
                : Flagged(VisitQrScanStatuses.Unverifiable, VisitExecutionFlags.QrUnverifiable, scannedAt);
        }

        var chain = await ChainAsync(tenantId, VisitExecutionRules.AccountOf(plan), cancellationToken);
        var active = chain.Count == 0
            ? new List<InstitutionQrCode>()
            : (await _codes.ListActiveByAccountsAsync(tenantId, chain, cancellationToken)).Where(c => c.IsActive() && !c.IsDeleted).ToList();

        return Decide(keys, tenantId, chain, active, text, required, scannedAt) with { ExpectedHasActiveCode = active.Count > 0 };
    }

    /// <summary>The verdict once the chain and its active codes are read (no further read: the pre-check and the start
    /// share it through <see cref="EvaluateAsync"/>).</summary>
    private static VisitQrEvaluation Decide(
        VisitQrKeys keys, Guid tenantId, List<Guid> chain, List<InstitutionQrCode> active, string? text, bool required, DateTimeOffset? scannedAt)
    {
        if (text is null)
        {
            if (active.Count == 0)
            {
                return required
                    ? Flagged(VisitQrScanStatuses.NotIssued, VisitExecutionFlags.QrNotIssued, scannedAt)
                    : Pass(VisitQrScanStatuses.NotIssued, scannedAt);
            }

            return required
                ? Refuse(VisitQrScanStatuses.Missing, scannedAt, new VisitExecutionRules.Failure(
                    "Scan the institution's QR code to start the visit.", VisitExecutionErrorCodes.QrRequired, 400))
                : Pass(VisitQrScanStatuses.Missing, scannedAt);
        }

        var verified = VisitQrSigner.Verify(keys, tenantId, text);
        if (verified is null)
        {
            return required
                ? Refuse(VisitQrScanStatuses.Invalid, scannedAt, new VisitExecutionRules.Failure(
                    "This is not a valid institution QR code.", VisitExecutionErrorCodes.QrInvalid, 400))
                : Flagged(VisitQrScanStatuses.Invalid, VisitExecutionFlags.QrInvalid, scannedAt);
        }

        var index = chain.IndexOf(verified.AccountId);
        if (index < 0)
        {
            var qr = new VisitExecutionQr { Status = VisitQrScanStatuses.WrongAccount, AccountId = verified.AccountId, Version = verified.Version, ScannedAt = scannedAt };
            return required
                ? new VisitQrEvaluation(qr, null, new VisitExecutionRules.Failure(
                    "This QR code belongs to another institution.", VisitExecutionErrorCodes.QrWrongAccount, 409,
                    VisitExecutionErrorCodes.ScannedAccountPrefix + verified.AccountId.ToString("D")), null)
                : new VisitQrEvaluation(qr, VisitExecutionFlags.QrWrongAccount, null, null);
        }

        var code = active.FirstOrDefault(c => c.AccountId == verified.AccountId && c.CodeVersion == verified.Version);
        if (code is null)
        {
            var qr = new VisitExecutionQr { Status = VisitQrScanStatuses.Revoked, AccountId = verified.AccountId, Version = verified.Version, ScannedAt = scannedAt };
            return required
                ? new VisitQrEvaluation(qr, null, new VisitExecutionRules.Failure(
                    "This QR code has been replaced or revoked. Scan the institution's current code.", VisitExecutionErrorCodes.QrRevoked, 409), null)
                : new VisitQrEvaluation(qr, VisitExecutionFlags.QrRevoked, null, null);
        }

        return new VisitQrEvaluation(new VisitExecutionQr
        {
            Status = VisitQrScanStatuses.Ok,
            AccountId = verified.AccountId,
            Version = verified.Version,
            ScannedAt = scannedAt,
            MatchedVia = index == 0 ? VisitQrMatchedVia.Self : VisitQrMatchedVia.Parent
        }, null, null, code.Id);
    }

    public async Task RecordScanAsync(Guid tenantId, VisitQrEvaluation evaluation, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (evaluation.MatchedCodeId is not { } codeId)
        {
            return;
        }

        try
        {
            await _codes.RecordScanAsync(tenantId, codeId, at, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The scan counter of institution QR code {CodeId} could not be updated; the visit start stands.", codeId);
        }
    }

    /// <summary>The visit's institution and its parents (≤ 5 above it), in order; a cycle or a missing parent stops the walk.</summary>
    public async Task<List<Guid>> ChainAsync(Guid tenantId, Guid? accountId, CancellationToken cancellationToken)
    {
        var chain = new List<Guid>();
        var current = accountId;
        while (current is { } id && id != Guid.Empty && !chain.Contains(id) && chain.Count <= VisitQrLimits.MaxParentDepth)
        {
            chain.Add(id);
            if (chain.Count > VisitQrLimits.MaxParentDepth)
            {
                break; // the institution + 5 parents; a 6th parent is never read
            }

            current = (await _accounts.GetByIdAsync(tenantId, id, cancellationToken))?.ParentAccountId;
        }

        return chain;
    }

    private static VisitQrEvaluation Pass(string status, DateTimeOffset? scannedAt)
        => new(new VisitExecutionQr { Status = status, ScannedAt = scannedAt }, null, null, null);

    private static VisitQrEvaluation Flagged(string status, string flag, DateTimeOffset? scannedAt)
        => new(new VisitExecutionQr { Status = status, ScannedAt = scannedAt }, flag, null, null);

    private static VisitQrEvaluation Refuse(string status, DateTimeOffset? scannedAt, VisitExecutionRules.Failure failure)
        => new(new VisitExecutionQr { Status = status, ScannedAt = scannedAt }, null, failure, null);
}
