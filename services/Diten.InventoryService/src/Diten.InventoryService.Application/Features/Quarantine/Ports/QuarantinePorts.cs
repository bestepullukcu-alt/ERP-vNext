using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;

namespace Diten.InventoryService.Application.Features.Quarantine.Ports;

/// <summary>Result of asking the approval side to start an approval for a submitted decision.</summary>
public sealed record ApprovalStart(bool Started, string? InstanceId, string? ErrorCode, int Status, string? Message)
{
    public static ApprovalStart Ok(string instanceId) => new(true, instanceId, null, 202, null);

    public static ApprovalStart Failed(string code, int status, string message) => new(false, null, code, status, message);
}

/// <summary>
/// Where a submitted decision goes for approval (pack §1 Q1: "driven by an internal approval port"). Slice Q1 ships two
/// implementations: <see cref="UnavailableApprovalPort"/> (the production default — no approval path exists before the
/// Workflow integration of Q2, so a submit fails closed with <c>QUARANTINE_WORKFLOW_UNAVAILABLE</c> and NOTHING moves:
/// DEC-INV-08, no release without workflow) and <see cref="InternalTestApprovalPort"/> (tests and the own-instance runtime
/// probe on a DB-010 test database only). Q2 replaces both by the Workflow client.
/// The outcome comes back through <c>QuarantineOutcomeApplier</c>.
/// </summary>
public interface IQuarantineApprovalPort
{
    Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default);
}

/// <summary>Production default of slice Q1: the Workflow integration is slice Q2 — refuse, fail-closed.</summary>
public sealed class UnavailableApprovalPort : IQuarantineApprovalPort
{
    public Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default)
        => Task.FromResult(ApprovalStart.Failed(QuarantineErrorCodes.WorkflowUnavailable, 503,
            "The approval workflow is not connected yet (MOD-0175 slice Q2); the decision was not sent and no stock moved."));
}

/// <summary>
/// Test approval port (Q1): accepts every submit with a synthetic instance id <c>test:{decisionId}</c>; the test (or the
/// probe) then reports the outcome through the test-approval endpoint. Registered only when
/// <c>Quarantine:TestApprovalPort:Enabled</c> is true AND the database is a DB-010 test database (startup refuses otherwise).
/// </summary>
public sealed class InternalTestApprovalPort : IQuarantineApprovalPort
{
    public const string InstancePrefix = "test:";

    public Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default)
        => Task.FromResult(ApprovalStart.Ok(InstancePrefix + decision.Id.ToString("D")));
}

/// <summary>Configuration of the test approval port (section <c>Quarantine:TestApprovalPort</c>).</summary>
public sealed class QuarantineTestApprovalOptions
{
    public const string SectionName = "Quarantine:TestApprovalPort";

    public bool Enabled { get; set; }
}

/// <summary>An active reservation of a SKU (CT-Q6 refusal details).</summary>
public sealed record ReservationLine(Guid ReservationId, string SourceModule, string? SourceDocumentId, decimal RemainingQuantity);

/// <summary>
/// Read-only ledger facts MOD-0175 needs that the ledger repository does not answer (pack §8 / ASSUMPTION-0175-05): the
/// transactions of a 0175 document, the SKU position and the active reservations of a SKU. Implemented in
/// <c>Persistence/Quarantine</c> with reads only — 0175 never writes a ledger collection (architecture-tested); every
/// posting goes through the ledger's internal posting service.
/// </summary>
public interface IQuarantineLedgerReader
{
    Task<IReadOnlyList<InventoryTransaction>> TransactionsAsync(
        IReadOnlyCollection<string> sourceDocumentIds, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);

    Task<decimal?> SkuUnreservedAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationLine>> ActiveReservationsAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);
}

/// <summary>A ledger transaction the intake sweep found (cross-tenant scan, read-only).</summary>
public sealed record IntakeCandidate(InventoryTransaction Transaction, Guid TenantId, Guid LegalEntityId);

/// <summary>Cross-tenant, read-only feed of recent inbound transactions into QUARANTINE / QUALITY_INSPECTION / BLOCKED (intake sweep).</summary>
public interface IQuarantineIntakeFeed
{
    Task<IReadOnlyList<IntakeCandidate>> RecentIntakesAsync(DateTimeOffset since, DateTimeOffset until, int limit, CancellationToken cancellationToken = default);
}
