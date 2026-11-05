using System.Text.Json;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

/// <summary>
/// The wire shape of a task (WMS draft §3.1 / §3.2) with its derived values: pending / remaining quantity, the one integration badge (§4.1)
/// and the server side of the screen rules (§8.15, AC-43): which actions the task's state allows and why the others are closed.
/// </summary>
public sealed class PutawayTaskReads
{
    private readonly PutawayTaskSupport _support;

    public PutawayTaskReads(PutawayTaskSupport support) => _support = support;

    public WarehouseTaskDto Dto(WarehouseTask task, int? confirmationSeq = null)
    {
        var (allowed, disabled) = Actions(task);
        return new WarehouseTaskDto
        {
            TaskId = task.Id.ToString("D"),
            Number = task.Number,
            CompanyCodePrefix = task.CompanyCodePrefix,
            WarehouseCode = task.WarehouseCode,
            Kind = task.Kind.ToString(),
            Status = task.Status.ToString(),
            Origin = task.Origin.ToString(),
            SourceRef = task.SourceRef is { } s ? new TaskSourceRefDto(s.SourceModule, s.SourceType, s.SourceDocumentId, s.SourceLineId, s.TransactionId) : null,
            SourceSystem = task.SourceSystem,
            ExternalRef = task.ExternalRef,
            LegalEntityId = (task.LegalEntityId ?? Guid.Empty).ToString("D"),
            WarehouseId = task.WarehouseId.ToString("D"),
            Item = new TaskItemDto(task.ItemId.ToString("D"), task.SkuId.ToString("D"), WireNames.ToWire(task.SkuLevel)),
            LotId = task.LotId,
            LotNumber = task.LotNumber,
            StockStatus = WireNames.ToWire(task.StockStatus),
            FromLocationId = task.FromLocationId.ToString("D"),
            ToLocationId = task.ToLocationId?.ToString("D"),
            SuggestedToLocationId = task.SuggestedToLocationId?.ToString("D"),
            TargetSource = task.TargetSource.ToString(),
            PlannedQuantity = PutawayTaskSupport.Q(task.PlannedQuantity),
            ConfirmedQuantity = PutawayTaskSupport.Q(task.ConfirmedQuantity),
            PendingQuantity = PutawayTaskSupport.Q(PutawayTaskRules.PendingQuantity(task)),
            RemainingQuantity = PutawayTaskSupport.Q(PutawayTaskRules.Remaining(task)),
            BaseUomId = task.BaseUomId,
            Assignee = task.AssigneeUserId is { } a ? new TaskPersonDto(a.ToString("D"), task.AssigneeName) : null,
            Priority = task.Priority.ToString(),
            Note = task.Note,
            SerialControlled = task.SerialControlled,
            Confirmations = task.Confirmations.OrderBy(c => c.Seq).Select(Confirmation).ToList(),
            IntegrationFlag = PutawayTaskRules.Flag(task, _support.Now(), _support.LongPending).ToString(),
            AllowedActions = allowed,
            DisabledActions = disabled,
            ShortCloseReason = task.ShortCloseReason,
            CancelReason = task.CancelReason,
            Created = new TaskStampDto(task.CreatedBy.ToString("D"), PutawayTaskSupport.Wire(task.CreatedAt), task.CreatedByName),
            Updated = task.UpdatedBy is { } u && task.UpdatedAt is { } at ? new TaskStampDto(u.ToString("D"), PutawayTaskSupport.Wire(at), task.UpdatedByName) : null,
            Version = task.Version,
            ConfirmationSeq = confirmationSeq,
            // DL-5 (WMS draft §11): a pick task names its outbound and allocation (never the ship-to, Q-10a)
            Outbound = task.Kind == WarehouseTaskKind.Pick && task.OutboundId is { } outboundId
                ? new PickTaskOutboundDto(outboundId.ToString("D"), task.OutboundNumber, null)
                : null,
            AllocSeq = task.Kind == WarehouseTaskKind.Pick ? task.AllocSeq : null,
            ReservationId = task.Kind == WarehouseTaskKind.Pick ? task.ReservationId?.ToString("D") : null
        };
    }

    private static TaskConfirmationDto Confirmation(TaskConfirmation c) => new(
        c.Seq,
        PutawayTaskSupport.Q(c.Quantity),
        c.ToLocationId.ToString("D"),
        c.Status.ToString(),
        c.PostingKey,
        PutawayTaskSupport.Wire(c.TransactionDate),
        PutawayTaskSupport.Wire(c.PostingDate),
        c.Channel.ToString(),
        new TaskPersonDto(c.ConfirmedBy.ToString("D"), c.ConfirmedByName),
        PutawayTaskSupport.Wire(c.ConfirmedAtUtc),
        c.Attempts,
        c.DeviationReason,
        c.SerialIds is { Count: > 0 } serials ? serials : null,
        c.TransactionId,
        c.TransactionNumber,
        c.ErrorCode is null && c.ErrorReason is null
            ? null
            : new TaskErrorDto(c.ErrorCode, c.ErrorMessage, c.ErrorDetailsJson is null ? null : JsonSerializer.Deserialize<JsonElement>(c.ErrorDetailsJson), c.ErrorStatus,
                c.ErrorReason),
        c.Resolution?.ToString(),
        c.PriorResolution?.ToString(),
        c.ResolutionReason,
        c.ScanRef,
        PutawayTaskSupport.Wire(c.LastAttemptAtUtc),
        c.RefusalSource?.ToString(),
        c.Status == ConfirmationStatus.Refused || c.RefusalSource is not null ? c.RefusedOnRetry : null,
        PutawayTaskSupport.Wire(c.RefusedAtUtc),
        PutawayTaskSupport.Wire(c.InFlightUntilUtc),
        c.IntegrityResolution?.ToString(),
        c.ResolveReason,
        c.HolderMovement is { } h
            ? new TaskHolderMovementDto(h.TransactionId, h.TransactionNumber, h.SourceModule, h.SourceType, h.SourceDocumentId, h.SourceLineId,
                PutawayTaskSupport.Q(h.Quantity), h.UomId, PutawayTaskSupport.Q(h.BaseQuantity), h.FromWarehouseId, h.FromLocationId, h.ToWarehouseId,
                h.ToLocationId, PutawayTaskSupport.Wire(h.PostingDate), h.LotId, h.SerialIds, h.FromStockStatus, h.ToStockStatus, h.ReversedByTransactionId)
            : null);

    /// <summary>
    /// The state rules of the task menu (§8.15, M-4 / AC-43): a Pending confirmation locks everything but Retry; an IntegrityError locks
    /// everything but Resolve; with no Posted confirmation only Cancel closes the task, with one only CloseShort (never for a serial SKU).
    /// </summary>
    public static (IReadOnlyList<string> Allowed, IReadOnlyList<DisabledActionDto> Disabled) Actions(WarehouseTask task)
    {
        var allowed = new List<string>();
        var disabled = new List<DisabledActionDto>();
        void Gate(string action, string? reason)
        {
            if (reason is null) allowed.Add(action);
            else disabled.Add(new DisabledActionDto(action, reason));
        }

        var open = PutawayTaskRules.IsOpen(task);
        var pending = PutawayTaskRules.HasPending(task);
        var integrity = PutawayTaskRules.HasIntegrityError(task);
        var lockReason = integrity ? "IntegrityError" : pending ? "ConfirmationPending" : null;
        var terminal = open ? null : "TaskTerminal";
        if (task.Kind == WarehouseTaskKind.Pick)
        {
            // DL-5 (§8.19): a pick task has no edit and no cancel of its own (the outbound's cancel closes it); "Kalanı kapat" also with 0 posted
            Gate("Confirm", terminal ?? lockReason);
            Gate("Claim", terminal ?? lockReason ?? (task.AssigneeUserId is null ? null : "AlreadyAssigned"));
            Gate("CloseShort", terminal ?? lockReason ?? (task.SerialControlled ? "SerialControlled" : null));
            Gate("Retry", pending ? null : "NoPendingConfirmation");
            Gate("MarkReturned", task.Confirmations.Any(PutawayTaskRules.IsUnresolvedRefused) ? (pending ? "ConfirmationPending" : null) : "NoRefusedConfirmation");
            Gate("ResolveIntegrity", integrity ? (pending ? "ConfirmationPending" : null) : "NoIntegrityError");
            return (allowed, disabled);
        }

        Gate("Confirm", terminal ?? lockReason);
        Gate("Update", terminal ?? lockReason);
        Gate("Claim", terminal ?? lockReason ?? (task.AssigneeUserId is null ? null : "AlreadyAssigned"));
        Gate("Cancel", terminal ?? lockReason ?? (PutawayTaskRules.HasPosted(task) ? "HasPostedConfirmations" : null));
        Gate("CloseShort", terminal ?? lockReason ?? (task.SerialControlled ? "SerialControlled" : PutawayTaskRules.HasPosted(task) ? null : "NoPostedConfirmation"));
        Gate("Retry", pending ? null : "NoPendingConfirmation");
        Gate("MarkReturned", task.Confirmations.Any(PutawayTaskRules.IsUnresolvedRefused) ? (pending ? "ConfirmationPending" : null) : "NoRefusedConfirmation");
        Gate("ResolveIntegrity", integrity ? (pending ? "ConfirmationPending" : null) : "NoIntegrityError");
        return (allowed, disabled);
    }
}
