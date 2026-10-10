using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

/// <summary>
/// MOD-0290-FU04 S2 (§3, §21) — the worker's command: the approval's decision applied to the item. Approved →
/// item.activated / item.reactivated; rejected → item.activation-rejected; not applied (self-approval, closed without a
/// decision, the activation gate no longer met) → item.activation-not-applicable. Sent by the approval processor under
/// its claim, through the pipeline, so the move is audited on path b with the decision's evidence; the tenant is the
/// command's own (a worker has no user token — IAuditTenantProvider).
///
/// <para>S2 FIX1 item 5: the same command carries the two other item moves the WORKER makes — the item pending on its
/// approval (<see cref="PendingBinding"/>) and a withdrawal the worker finishes (<see cref="Withdrawal"/>) — so no worker
/// move is unaudited. The actor is always the system (<see cref="IAuditSystemActor"/>: an approver's decision applied in
/// the maker's replay is not the maker's act; who decided is in the metadata). A refusal is recorded once per operation
/// and again only when its code changes (<see cref="IAuditFailureRepeatFilter"/>), never once per retry.</para>
/// </summary>
public sealed record ApplyItemActivationDecisionCommand(
    Guid ItemId,
    Guid TenantId,
    ItemWorkflowClaim Claim,
    ItemWorkflowOperation Operation,
    ProductIdentityWorkflowDecisionEvidence? Evidence,
    string? NotApplicableReason,
    long UpdatedAtUtcTicks,
    ProductIdentityWorkflowBinding? PendingBinding = null,
    ProductIdentityWorkflowCancellationEvidence? Withdrawal = null)
    : IRequest<Response<ItemWorkflowWriteResult>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider,
        IAuditTenantProvider, IAuditSystemActor, IAuditFailureRepeatFilter
{
    // The failure this operation last recorded (a wait at its limit keeps its cause aside).
    private readonly string? _previousFailure =
        Operation.LastFailureCode == Activation.ItemActivationWorkflowProcessor.RetryLimitReached
            ? Operation.RetryLimitCauseCode
            : Operation.LastFailureCode;

    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(
        Evidence?.Decision == ProductIdentityDecisionKind.Approved && NotApplicableReason is null
            ? Operation.ActivationKind == ItemActivationKind.Reactivation ? AuditOperation.Reactivate : AuditOperation.Activate
            : AuditOperation.Update);

    public AuditDetails AuditDetails { get; } = new();
    public Guid AuditTenantId => TenantId;

    public bool ShouldAuditFailure(string? refusalCode) =>
        !string.Equals(Normalized(refusalCode), Normalized(_previousFailure), StringComparison.Ordinal);

    // The processor files a version conflict as a concurrency conflict (WriteRefusedAsync); one fact, one code.
    private static string? Normalized(string? code) => code == "ITEM_VERSION_CONFLICT" ? "ITEM_CONCURRENCY_CONFLICT" : code;
}
