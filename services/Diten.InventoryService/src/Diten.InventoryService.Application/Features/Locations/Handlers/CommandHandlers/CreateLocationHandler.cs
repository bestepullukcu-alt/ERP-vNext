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
/// createLocation (MOD-0173-FU02 §15). Order:
/// <list type="number">
/// <item>Shape (400) — key, code, name, type, stockable / operating mode by type, org-unit / cost-centre shape.</item>
/// <item>Idempotency — a committed key answers from the stored node: same fingerprint → 200 replay, else 400.</item>
/// <item>Org unit with Platform (503 / 422), before any write.</item>
/// <item>One transaction: parent in the caller's company (404 / 403), not Archived (409), K-01 level (422), Active
/// (K-06, 422); code free (409, also enforced by the unique index); insert + parent structure touch + one
/// <c>Created</c> history row (K-11).</item>
/// </list>
/// The company is never a field: the node takes the scope company (K-03), so a child always has its parent's.
/// </summary>
public sealed class CreateLocationHandler : IRequestHandler<CreateLocationCommand, Response<LocationDetailDto>>
{
    private readonly ILocationRepository _locations;
    private readonly LocationWriteSupport _support;

    public CreateLocationHandler(ILocationRepository locations, LocationWriteSupport support)
    {
        _locations = locations;
        _support = support;
    }

    public async Task<Response<LocationDetailDto>> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var keyError = LocationWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return LocationWriteSupport.Invalid<LocationDetailDto>(keyError);

        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var costCenter = request.CostCenterCode?.Trim();
        if (LocationWriteSupport.CodeError(code) is { } codeError) return LocationWriteSupport.Invalid<LocationDetailDto>(codeError);
        if (LocationWriteSupport.NameError(name) is { } nameError) return LocationWriteSupport.Invalid<LocationDetailDto>(nameError);
        if (!LocationWriteSupport.TryParseType(request.Type, out var type))
            return LocationWriteSupport.Invalid<LocationDetailDto>("type is required: Site, Warehouse, StorageLocation, Zone or Bin.");

        Guid? parentId = null;
        if (request.ParentId is not null)
        {
            if (!LocationWriteSupport.TryParseId(request.ParentId, out var parsed))
                return LocationWriteSupport.Invalid<LocationDetailDto>("parentId must be a UUID.");
            parentId = parsed;
        }

        var stockable = request.IsStockable ?? LocationHierarchyRules.DefaultStockable(type);
        if (LocationWriteSupport.StockableError(type, stockable) is { } stockableError) return LocationWriteSupport.Invalid<LocationDetailDto>(stockableError);
        if (LocationWriteSupport.OperatingModeError(type, request.OperatingMode) is { } modeError) return LocationWriteSupport.Invalid<LocationDetailDto>(modeError);
        if (LocationWriteSupport.OrgUnitShapeError(request.OrgUnitRef) is { } orgError) return LocationWriteSupport.Invalid<LocationDetailDto>(orgError);
        if (LocationWriteSupport.CostCenterError(costCenter) is { } costError) return LocationWriteSupport.Invalid<LocationDetailDto>(costError);

        // DEC-INV-19: a Site / Warehouse without an explicit mode is run internally (ASSUMPTION-LOC1-05).
        var operatingMode = LocationHierarchyRules.MayHaveOperatingMode(type)
            ? request.OperatingMode ?? LocationHierarchyRules.OperatingModeInternal
            : null;

        var fingerprint = LocationWriteSupport.Fingerprint(
            ("code", code), ("name", name), ("type", type.ToString()), ("parentId", parentId?.ToString("D")),
            ("isStockable", LocationWriteSupport.Wire(stockable)), ("operatingMode", operatingMode),
            ("orgUnitRef", request.OrgUnitRef?.ToLowerInvariant()), ("costCenterCode", costCenter));

        var committed = await _locations.FindByCreateKeyAsync(key, cancellationToken);
        if (committed is not null) return Replay(committed, fingerprint);

        // K-01 without a parent needs no read: only a Site stands alone, and a Site has no parent.
        if ((parentId is null && !LocationHierarchyRules.IsAllowedParent(type, null)) || (parentId is not null && type == LocationType.Site))
        {
            return LevelInvalid(type);
        }

        if (await _support.OrgUnitRefusalAsync<LocationDetailDto>(request.OrgUnitRef?.ToLowerInvariant(), cancellationToken) is { } orgRefusal)
        {
            return orgRefusal;
        }

        Response<LocationDetailDto> outcome;
        try
        {
            outcome = await _locations.InTransactionAsync(async (tx, ct) =>
            {
                var now = DateTimeOffset.UtcNow;
                var ancestors = new List<Guid>();
                if (parentId is { } pid)
                {
                    var parent = await tx.FindAsync(pid, ct);
                    if (parent is null) return await _support.MissingParentAsync<LocationDetailDto>(pid, ct);
                    if (parent.Status == LocationStatus.Archived)
                        return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.Archived, "The parent location is archived.", 409);
                    if (!LocationHierarchyRules.IsAllowedParent(type, parent.Type)) return LevelInvalid(type, parent.Type);
                    if (parent.Status != LocationStatus.Active)
                        return LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.ParentInactive, "The parent location is not active.", 422);
                    ancestors = [.. parent.AncestorIds, parent.Id];
                }

                var normalized = LocationHierarchyRules.NormalizeCode(code!);
                if ((await tx.TakenCodesAsync([normalized], ct)).Count > 0) return CodeTaken(code!);

                var location = new Location
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    Code = code!,
                    CodeNormalized = normalized,
                    Name = name!,
                    Type = type,
                    ParentId = parentId,
                    AncestorIds = ancestors,
                    Status = LocationStatus.Active,
                    IsStockable = stockable,
                    OperatingMode = operatingMode,
                    OrgUnitRef = request.OrgUnitRef?.ToLowerInvariant(),
                    CostCenterCode = costCenter,
                    Version = 1,
                    CreatedBy = _support.UserId,
                    CreateIdempotencyKey = key,
                    CreateRequestFingerprint = fingerprint
                };

                await tx.InsertAsync([location], ct);
                if (parentId is { } attachedTo) await tx.TouchStructureAsync(attachedTo, ct);
                await tx.AppendHistoryAsync([_support.History(location.Id, LocationChangeAction.Created, null, null, location.Code, now)], ct);

                return Response<LocationDetailDto>.Success(LocationMapping.ToDetail(location), 201);
            }, cancellationToken);
        }
        catch (LocationUniqueViolationException violation) when (violation.Index == LocationUniqueIndex.Code)
        {
            outcome = CodeTaken(code!);
        }
        catch (LocationUniqueViolationException violation) when (violation.Index == LocationUniqueIndex.CreateIdempotencyKey)
        {
            // A concurrent request committed this key between our lookup and our insert.
            var winner = await _locations.FindByCreateKeyAsync(key, cancellationToken)
                         ?? throw new InvalidOperationException("Create key reported taken but no location carries it.");
            return Replay(winner, fingerprint);
        }

        // A concurrent request with the SAME key may have committed this very code first: that is a replay, not a clash.
        if (outcome.ErrorCode == LocationErrorCodes.CodeTaken
            && await _locations.FindByCreateKeyAsync(key, cancellationToken) is { } sameKey)
        {
            return Replay(sameKey, fingerprint);
        }

        return outcome;
    }

    private static Response<LocationDetailDto> Replay(Location committed, string fingerprint)
        => string.Equals(committed.CreateRequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<LocationDetailDto>.Success(LocationMapping.ToDetail(committed), 200)
            : LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different location.", 400);

    private static Response<LocationDetailDto> CodeTaken(string code)
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.CodeTaken,
            $"The code '{code}' is already used in this company.", 409);

    private static Response<LocationDetailDto> LevelInvalid(LocationType type, LocationType? parentType = null)
        => LocationWriteSupport.Fail<LocationDetailDto>(LocationErrorCodes.LevelInvalid,
            parentType is null
                ? type == LocationType.Site ? "A Site has no parent." : $"A {type} needs a parent."
                : $"A {type} cannot be placed under a {parentType}.",
            422);
}
