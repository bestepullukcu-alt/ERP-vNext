using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers;

/// <summary>
/// MOD-0040-FU02 DL-1 — FIRST COMMIT GROUP (AC-41): the actor probe. Only the audit record is written, inside the
/// production transaction executor, through the production in-transaction door; no counter, MDM or ledger yet, and the
/// answer is a fixed probe code. What it measures: the record's actor is the forwarded user (TenantUser + sub).
/// </summary>
public sealed class AllocateRecordCodeHandler : IRequestHandler<AllocateRecordCodeCommand, Response<RecordCodeAllocationResult>>
{
    public const string ProbeCode = "PROBE-ACT-000000";

    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _auditWriter;

    public AllocateRecordCodeHandler(IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter auditWriter)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
    }

    public async Task<Response<RecordCodeAllocationResult>> Handle(
        AllocateRecordCodeCommand request,
        CancellationToken cancellationToken)
    {
        var inserted = await _transactions.ExecuteAsync(
            (session, token) =>
            {
                var allocationId = Guid.NewGuid();
                return RecordCodeAuditIntent.EnqueueAsync(
                    _auditWriter,
                    session,
                    request.TenantId,
                    allocationId,
                    new Dictionary<string, object?> { ["Code"] = ProbeCode, ["Sequence"] = 0L },
                    new Dictionary<string, object?> { ["CallerService"] = request.CallerService },
                    token);
            },
            cancellationToken);

        return inserted
            ? Response<RecordCodeAllocationResult>.Success(new RecordCodeAllocationResult(ProbeCode, 0))
            : Response<RecordCodeAllocationResult>.Fail("The audit record was not written.", 503, "RECORD_CODE_UNAVAILABLE");
    }
}
