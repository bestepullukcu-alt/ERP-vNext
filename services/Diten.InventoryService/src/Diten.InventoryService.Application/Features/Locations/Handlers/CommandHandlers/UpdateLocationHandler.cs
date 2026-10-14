using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Commands;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.CommandHandlers;

/// <summary>
/// updateLocation (PATCH, MOD-0173-FU02 §15). Refusal order: missing <c>If-Match</c> / malformed member → 400; not in
/// the caller's company → 404; Archived → 409 <c>LOCATION_ARCHIVED</c>; stale ETag → 409 <c>LOCATION_CONFLICT</c>; then
/// per field: code taken (409), stockable / operating mode against stock (422, K-08 / K-09), move rules (K-07: own
/// subtree, level, parent Active, stock). Everything — node, subtree paths, parent touch, one history row per changed
/// field — commits in ONE transaction; a concurrent ledger posting to the node conflicts with it and the loser re-runs
/// on the committed state. The id never changes, so ledger rows keep resolving after a code edit (K-02).
/// </summary>
public sealed class UpdateLocationHandler : IRequestHandler<UpdateLocationCommand, Response<LocationDetailDto>>
{
    private readonly ILocationRepository _locations;
    private readonly LocationWriteSupport _support;

    public UpdateLocationHandler(ILocationRepository locations, LocationWriteSupport support)
    {
        _locations = locations;
        _support = support;
    }

    public async Task<Response<LocationDetailDto>> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IfMatch))
            return LocationWriteSupport.Invalid<LocationDetailDto>("The If-Match header is required.");
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var locationId))
            return LocationWriteSupport.NotFound<LocationDetailDto>();

        // ── shape (400), independent of the stored node ──
        var code = request.Code.Value?.Trim();
        var name = request.Name.Value?.Trim();
        var costCenter = request.CostCenterCode.Value?.Trim();
        if (request.Code.IsPresent && LocationWriteSupport.CodeError(code) is { } codeError) return LocationWriteSupport.Invalid<LocationDetailDto>(codeError);
        if (request.Name.IsPresent && LocationWriteSupport.NameError(name) is { } nameError) return LocationWriteSupport.Invalid<LocationDetailDto>(nameError);
        if (request.IsStockable.IsPresent && request.IsStockable.Value is null)
            return LocationWriteSupport.Invalid<LocationDetailDto>("isStockable must be true or false.");
        if (request.CostCenterCode.IsPresent && LocationWriteSupport.CostCenterError(costCenter) is { } costError)
            return LocationWriteSupport.Invalid<LocationDetailDto>(costError);
        if (request.OrgUnitRef.IsPresent && LocationWriteSupport.OrgUnitShapeError(request.OrgUnitRef.Value) is { } orgError)
            return LocationWriteSupport.Invalid<LocationDetailDto>(orgError);
        if (request.OperatingMode.IsPresent && request.OperatingMode.Value is not null
            && !LocationHierarchyRules.IsValidOperatingMode(request.OperatingMode.Value))
            return LocationWriteSupport.Invalid<LocationDetailDto>("operatingMode must be internal or external-integrated.");

        Guid? newParentId = null;
        if (request.ParentId.IsPresent && request.ParentId.Value is not null)
        {
            if (!LocationWriteSupport.TryParseId(request.ParentId.Value, out var parsed))
                return LocationWriteSupport.Invalid<LocationDetailDto>("parentId must be a UUID.");
            newParentId = parsed;
        }

        var orgUnitRef = request.OrgUnitRef.Value?.ToLowerInvariant();
        var expectedVersion = LocationWriteSupport.ExpectedVersion(request.IfMatch);

        // Org unit: checked with Platform only when it actually changes (one HTTP call, outside the transaction).
        var current = await _locations.FindAsync(locationId, cancellationToken);
        if (current is null) return LocationWriteSupport.NotFound<LocationDetailDto>();
        if (request.OrgUnitRef.IsPresent && orgUnitRef is not null && !string.Equals(orgUnitRef, current.OrgUnitRef, StringComparison.Ordinal)
            && current.Status != LocationStatus.Archived
            && await _support.OrgUnitRefusalAsync<LocationDetailDto>(orgUnitRef, cancellationToken) is { } orgRefusal)
        {
            return orgRefusal;
        }

        try
        {
            return await _locations.InTransactionAsync(async (tx, ct) =>
            {
                var node = await tx.FindAsync(locationId, ct);
                if (node is null) return LocationWriteSupport.NotFound<LocationDetailDto>();
                if (node.Status == LocationStatus.Archived)
                    return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Archived, "The location is archived and cannot be changed.", 409);
                if (node.Version != expectedVersion) return Conflict();

                var now = DateTimeOffset.UtcNow;
                var history = new List<LocationChangeEntry>();
                LocationStockPresence? presence = null;
                async Task<LocationStockPresence> Presence() => presence ??= await tx.StockPresenceAsync(node, ct);

                void Changed(LocationChangeAction action, string field, string? oldValue, string? newValue)
                    => history.Add(_support.History(node.Id, action, field, oldValue, newValue, now));

                // ── code (K-02): always editable, unique per company ──
                if (request.Code.IsPresent && !string.Equals(code, node.Code, StringComparison.Ordinal))
                {
                    var normalized = LocationHierarchyRules.NormalizeCode(code!);
                    if (!string.Equals(normalized, node.CodeNormalized, StringComparison.Ordinal)
                        && (await tx.TakenCodesAsync([normalized], ct)).Count > 0)
                    {
                        return CodeTaken(code!);
                    }

                    Changed(LocationChangeAction.Updated, "code", node.Code, code);
                    node.Code = code!;
                    node.CodeNormalized = normalized;
                }

                if (request.Name.IsPresent && !string.Equals(name, node.Name, StringComparison.Ordinal))
                {
                    Changed(LocationChangeAction.Updated, "name", node.Name, name);
                    node.Name = name!;
                }

                // ── stockable (K-08) ──
                if (request.IsStockable.IsPresent && request.IsStockable.Value != node.IsStockable)
                {
                    var stockable = request.IsStockable.Value!.Value;
                    if (LocationWriteSupport.StockableError(node.Type, stockable) is { } error) return LocationWriteSupport.Invalid<LocationDetailDto>(error);
                    if (!stockable && (await Presence()).HasStock) return HasStock("The location holds stock; it cannot stop being stockable.");
                    Changed(LocationChangeAction.Updated, "isStockable", LocationWriteSupport.Wire(node.IsStockable), LocationWriteSupport.Wire(stockable));
                    node.IsStockable = stockable;
                }

                // ── operating mode (K-09) ──
                if (request.OperatingMode.IsPresent && !string.Equals(request.OperatingMode.Value, node.OperatingMode, StringComparison.Ordinal))
                {
                    if (LocationWriteSupport.OperatingModeError(node.Type, request.OperatingMode.Value ?? LocationHierarchyRules.OperatingModeInternal) is { } error)
                        return LocationWriteSupport.Invalid<LocationDetailDto>(error);
                    if ((await Presence()).Any) return HasStock("Stock is held at or below this location; its operating mode is locked.");
                    Changed(LocationChangeAction.Updated, "operatingMode", node.OperatingMode, request.OperatingMode.Value);
                    node.OperatingMode = request.OperatingMode.Value;
                }

                if (request.OrgUnitRef.IsPresent && !string.Equals(orgUnitRef, node.OrgUnitRef, StringComparison.Ordinal))
                {
                    Changed(LocationChangeAction.Updated, "orgUnitRef", node.OrgUnitRef, orgUnitRef);
                    node.OrgUnitRef = orgUnitRef;
                }

                if (request.CostCenterCode.IsPresent && !string.Equals(costCenter, node.CostCenterCode, StringComparison.Ordinal))
                {
                    Changed(LocationChangeAction.Updated, "costCenterCode", node.CostCenterCode, costCenter);
                    node.CostCenterCode = costCenter;
                }

                // ── move (K-07) ──
                IReadOnlyList<(Guid Id, List<Guid> Ancestors)> rewrites = [];
                if (request.ParentId.IsPresent && newParentId != node.ParentId)
                {
                    if (newParentId is null || node.Type == LocationType.Site) return LevelInvalid(node.Type, null);

                    var parent = await tx.FindAsync(newParentId.Value, ct);
                    if (parent is null) return await _support.MissingParentAsync<LocationDetailDto>(newParentId.Value, ct);
                    if (parent.Id == node.Id || parent.AncestorIds.Contains(node.Id))
                        return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.MoveIntoSubtree,
                            "A location cannot be moved under itself or one of its descendants.", 422);
                    if (parent.Status == LocationStatus.Archived)
                        return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Archived, "The new parent location is archived.", 409);
                    if (!LocationHierarchyRules.IsAllowedParent(node.Type, parent.Type)) return LevelInvalid(node.Type, parent.Type);
                    if (parent.Status != LocationStatus.Active)
                        return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.ParentInactive, "The new parent location is not active.", 422);
                    if ((await Presence()).Any) return HasStock("Stock is held at or below this location; it cannot be moved.");

                    var oldPrefix = node.AncestorIds.Count;
                    List<Guid> newAncestors = [.. parent.AncestorIds, parent.Id];
                    rewrites = (await tx.SubtreeAsync(node.Id, ct))
                        .Where(d => d.Id != node.Id)
                        .Select(d => (d.Id, (List<Guid>)[.. newAncestors, .. d.AncestorIds.Skip(oldPrefix)]))
                        .ToList();

                    Changed(LocationChangeAction.Moved, "parentId", node.ParentId?.ToString("D"), parent.Id.ToString("D"));
                    node.ParentId = parent.Id;
                    node.AncestorIds = newAncestors;
                    await tx.TouchStructureAsync(parent.Id, ct);
                }

                if (history.Count == 0)
                {
                    // Nothing changed: no new version, no history row.
                    return Response<LocationDetailDto>.Success(LocationMapping.ToDetail(node), 200);
                }

                node.UpdatedAt = now;
                node.UpdatedBy = _support.UserId;
                if (!await tx.ReplaceAsync(node, expectedVersion, ct)) return Conflict();
                foreach (var (id, ancestors) in rewrites)
                {
                    await tx.SetAncestorsAsync(id, ancestors, ct);
                }

                await tx.AppendHistoryAsync(history, ct);
                return Response<LocationDetailDto>.Success(LocationMapping.ToDetail(node), 200);
            }, cancellationToken);
        }
        catch (LocationUniqueViolationException violation) when (violation.Index == LocationUniqueIndex.Code)
        {
            return CodeTaken(code!);
        }
    }

    private static Response<LocationDetailDto> Conflict()
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Conflict,
            "The location changed since it was read; reload it and retry with its current ETag.", 409);

    private static Response<LocationDetailDto> CodeTaken(string code)
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.CodeTaken, $"The code '{code}' is already used in this company.", 409);

    private static Response<LocationDetailDto> HasStock(string message)
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.HasStock, message, 422);

    private static Response<LocationDetailDto> LevelInvalid(LocationType type, LocationType? parentType)
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.LevelInvalid,
            parentType is null
                ? type == LocationType.Site ? "A Site has no parent." : $"A {type} needs a parent."
                : $"A {type} cannot be placed under a {parentType}.",
            422);
}
