using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.RecordCodes.Commands;

/// <summary>
/// MOD-0040-FU02 §4 "Yönetici ucu" — align the tenant's group-prefix setting to the effective document <c>OrgPrefix</c>.
/// Bodyless: <see cref="BodyPresent"/> is only the controller's measurement that a body was sent, so the refusal is given —
/// and recorded — in the handler. <see cref="ITransactionOwnedAuditCommand"/>: the handler writes the record in the
/// setting's own transaction (named in <c>TransactionOwnedAuditCommands.Authorized</c>, CT decision 2026-11-02).
/// No validator: a validator's refusal would never reach the audit trail (AUD-001 §3).
/// </summary>
public sealed record AlignRecordCodeGroupPrefixCommand(Guid TenantId, bool BodyPresent)
    : IRequest<Response<RecordCodeGroupPrefixDto>>, ITransactionOwnedAuditCommand;
