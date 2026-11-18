using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PickTasks.Commands;

// MOD-0178 DL-5 — the S2 pick task commands (pack §3, §8.19; WMS draft §11). The confirmation machine is S1's (§8.3); only the movement fields and
// the rules change with the kind. "Supervisor" = the caller holds warehouse.pick-tasks.update (from the token, never the body). One central
// record per command (pack §21, the S1 rows).

/// <summary><c>claimPickTask</c> — an operator assigns an unassigned pick task to himself or herself (M-5).</summary>
public sealed record ClaimPickTaskCommand(Guid TaskId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Assign", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}

/// <summary><c>bulkAssignPickTasks</c> — a supervisor assigns up to 100 pick tasks, each at its own version (the S1 bulk shape).</summary>
public sealed record BulkAssignPickTaskCommand(Guid AssigneeUserId, IReadOnlyList<BulkAssignItem> Items, string IdempotencyKey)
    : IRequest<Response<BulkAssignResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Assign", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => null;
}

/// <summary><c>confirmPickTask</c> — bin → Staging <c>LOCATION_TRANSFER</c> with the allocation's <c>reservationId</c> (validated, not consumed).</summary>
public sealed record ConfirmPickTaskCommand(Guid TaskId, ConfirmPickTaskInput Confirmation, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}

/// <summary><c>retryPickConfirmation</c> — a Pending pick confirmation, with the SAME key, dates and reservation.</summary>
public sealed record RetryPickConfirmationCommand(Guid TaskId, int Seq, string IdempotencyKey, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}

/// <summary><c>markPickConfirmationReturned</c> — "Geri taşındı" for an unresolved Refused pick confirmation (no movement).</summary>
public sealed record MarkPickConfirmationReturnedCommand(Guid TaskId, int Seq, string? Reason, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}

/// <summary><c>resolvePickIntegrity</c> — "Uzlaştır" of an IntegrityError pick confirmation (<c>warehouse.pick-tasks.resolve</c>).</summary>
public sealed record ResolvePickIntegrityCommand(Guid TaskId, int Seq, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}

/// <summary><c>closePickTaskShort</c> — Completed with the remainder left (also with nothing picked); the reservation does NOT shrink (§8.19).</summary>
public sealed record ClosePickTaskShortCommand(Guid TaskId, string? Reason, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
