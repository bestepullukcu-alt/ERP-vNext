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
/// </summary>
public sealed record ApplyItemActivationDecisionCommand(
    Guid ItemId,
    Guid TenantId,
    ItemWorkflowClaim Claim,
    ItemWorkflowOperation Operation,
    ProductIdentityWorkflowDecisionEvidence? Evidence,
    string? NotApplicableReason,
    long UpdatedAtUtcTicks)
    : IRequest<Response<ItemWorkflowWriteResult>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider,
        IAuditTenantProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(
        Evidence?.Decision == ProductIdentityDecisionKind.Approved && NotApplicableReason is null
            ? Operation.ActivationKind == ItemActivationKind.Reactivation ? AuditOperation.Reactivate : AuditOperation.Activate
            : AuditOperation.Update);

    public AuditDetails AuditDetails { get; } = new();
    public Guid AuditTenantId => TenantId;
}
