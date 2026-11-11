using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.HandlingUnits;

/// <summary>A parsed package body (weight kg, dimensions, contents) — or the 400 message.</summary>
internal sealed record ParsedPackage(decimal? WeightKg, string? DimensionsCm, IReadOnlyList<HandlingUnitContent>? Contents);

/// <summary>
/// The packing commands (pack §8.20; AC-29, AC-30, AC-33 (v), AC-72). Packing writes no movement and never calls INVENTORY. Every package write
/// also writes its outbound (version + 1, the timeline row) with a compare-and-set, so two concurrent package writes of one outbound never both
/// pass the "Σ in packages ≤ picked" check (the loser re-reads: replay or 409). The key events live on the outbound timeline (<c>RequestOp</c>
/// separates the package operations); the create key is on the package.
/// </summary>
public abstract class HandlingUnitHandler
{
    protected HandlingUnitHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support)
    {
        Outbounds = outbounds;
        Reads = reads;
        Support = support;
    }

    protected IOutboundShipmentRepository Outbounds { get; }

    protected OutboundShipmentReads Reads { get; }

    protected WarehouseSupport Support { get; }

    internal static (ParsedPackage? Package, string? Error) Parse(PackageInput input, bool create)
    {
        decimal? weight = null;
        if (input.HasWeight && input.WeightKg is not null)
        {
            if (!InventoryQuantity.TryParsePositive(input.WeightKg, out var w) || !OutboundFlowRules.IsWeight(w)) return (null, "weightKg must be a decimal > 0 with at most 3 fraction digits.");
            weight = w;
        }

        var dimensions = WarehouseSupport.Trimmed(input.DimensionsCm);
        if (input.HasDimensions && dimensions is not null && !OutboundFlowRules.IsDimensions(dimensions)) return (null, "dimensionsCm must be LxWxH in whole centimetres (e.g. 12x30x40).");

        List<HandlingUnitContent>? contents = null;
        if (input.HasContents || create)
        {
            contents = [];
            foreach (var c in input.Contents ?? [])
            {
                if (c.AllocSeq < 1) return (null, "contents[].allocSeq must be ≥ 1.");
                if (!InventoryQuantity.TryParsePositive(c.Quantity, out var q)) return (null, "contents[].quantity must be a positive decimal with at most 6 fraction digits.");
                if (contents.Any(x => x.AllocSeq == c.AllocSeq)) return (null, "contents must not repeat an allocSeq.");
                contents.Add(new HandlingUnitContent { AllocSeq = c.AllocSeq, Quantity = q });
            }
        }

        return (new ParsedPackage(weight, dimensions, contents), null);
    }

    internal static string Fingerprint(string op, string id, PackageInput input, ParsedPackage p)
        => WarehouseSupport.Fingerprint(("op", op), ("id", id),
            ("weightKg", input.HasWeight ? "1:" + (p.WeightKg is { } w ? InventoryQuantity.ToWire(w) : null) : null),
            ("dimensionsCm", input.HasDimensions ? "1:" + p.DimensionsCm : null),
            ("contents", p.Contents is null ? null : WarehouseSupport.List(p.Contents.OrderBy(c => c.AllocSeq).Select(c => $"{c.AllocSeq}:{InventoryQuantity.ToWire(c.Quantity)}"))));

    /// <summary>§8.18 state × command for the package commands: Picked only; ReadyToShip / Shipped → IMMUTABLE; Allocated / Packed / Cancelled → T.</summary>
    protected static Response<T>? State<T>(OutboundShipment o)
        => o.Status switch
        {
            OutboundStatus.Picked => null,
            OutboundStatus.ReadyToShip or OutboundStatus.Shipped => WarehouseSupport.Fail<T>(WarehouseErrorCodes.OutboundImmutable,
                "A ready-to-ship outbound does not change any more.", 409, new Dictionary<string, object?> { ["status"] = o.Status.ToString() }),
            _ => WarehouseSupport.Fail<T>(WarehouseErrorCodes.InvalidTransition, $"Packages change only on a Picked outbound (this one is {o.Status}).", 409,
                new Dictionary<string, object?> { ["status"] = o.Status.ToString() })
        };

    /// <summary>§8.20: every content's allocation is the outbound's and Σ (open + closed, this package's new content) ≤ its picked quantity.</summary>
    protected static Response<T>? Fits<T>(OutboundShipment o, IReadOnlyDictionary<int, decimal> picked, IReadOnlyList<HandlingUnit> packages,
        IReadOnlyList<HandlingUnitContent> contents, Guid? except)
    {
        foreach (var c in contents)
        {
            if (o.AllAllocations().All(a => a.AllocSeq != c.AllocSeq)) return WarehouseSupport.Invalid<T>("contents[].allocSeq must name an allocation of this outbound.");
            var already = OutboundFlowRules.InPackages(packages, c.AllocSeq, except);
            var have = picked.GetValueOrDefault(c.AllocSeq);
            if (already + c.Quantity > have)
            {
                return WarehouseSupport.Fail<T>(WarehouseErrorCodes.PackageExceedsPicked, "The packages would hold more than was picked for the allocation.", 422,
                    new Dictionary<string, object?>
                    {
                        ["allocSeq"] = c.AllocSeq, ["picked"] = InventoryQuantity.ToWire(have), ["inPackages"] = InventoryQuantity.ToWire(already),
                        ["requested"] = InventoryQuantity.ToWire(c.Quantity)
                    });
            }
        }

        return null;
    }

    protected OutboundShipment Touched(OutboundShipment outbound, DateTimeOffset now)
    {
        var copy = outbound.Copy();
        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        return copy;
    }

    protected static Dictionary<string, string?> Data(HandlingUnit p) => new()
    {
        ["packageId"] = p.Id.ToString("D"), ["packageNumber"] = p.PackageNumber,
        ["contents"] = JsonSerializer.Serialize(p.Contents.Select(c => new { c.AllocSeq, quantity = InventoryQuantity.ToWire(c.Quantity) })),
        ["weightKg"] = p.WeightKg?.ToString("0.000", CultureInfo.InvariantCulture), ["dimensionsCm"] = p.DimensionsCm
    };

    protected static Response<PackageDto> NotFound() => Response<PackageDto>.Fail(WarehouseErrorCodes.PackageNotFound, "Package not found.", 404);

    /// <summary>The key of a package command (update / close) on its outbound's timeline: same body → 200 + the current package.</summary>
    protected async Task<Response<PackageDto>?> ReplayAsync(HandlingUnit package, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Outbounds.FindKeyEventAsync(package.OutboundId, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<PackageDto>();
        var current = await Outbounds.FindPackageAsync(package.Id, ct);
        return current is null ? NotFound() : Response<PackageDto>.Success(OutboundShipmentReads.Package(current));
    }
}

/// <summary><c>createPackage</c>: shape (400); the create key (replay 200 / 400); the outbound (404) and its state; Σ ≤ picked; the PKG number; one write.</summary>
public sealed class CreateHandlingUnitHandler : HandlingUnitHandler, IRequestHandler<CreateHandlingUnitCommand, Response<PackageDto>>
{
    private readonly IWarehouseRecordCodeAllocator _numbers;

    public CreateHandlingUnitHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support, IWarehouseRecordCodeAllocator numbers)
        : base(outbounds, reads, support) => _numbers = numbers;

    public async Task<Response<PackageDto>> Handle(CreateHandlingUnitCommand request, CancellationToken ct)
    {
        var (parsed, error) = Parse(request.Package, create: true);
        if (parsed is null) return WarehouseSupport.Invalid<PackageDto>(error!);
        var fingerprint = Fingerprint(WarehouseContract.OpCreatePackage, request.OutboundId.ToString("D"), request.Package, parsed);
        if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var outbound = await Reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<PackageDto>();
        if (State<PackageDto>(outbound) is { } state) return state;
        var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
        var packages = await Outbounds.PackagesAsync(outbound.Id, ct);
        if (Fits<PackageDto>(outbound, OutboundFlowRules.PickedByAllocation(tasks), packages, parsed.Contents!, null) is { } refusal) return refusal;

        string number;
        try
        {
            number = await _numbers.NextAsync(outbound.CompanyCodePrefix, WarehouseRecordCodeRules.Package, ct);
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return WarehouseSupport.Fail<PackageDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The package number could not be issued; nothing was written.", 503);
        }

        var now = Support.Now();
        var package = new HandlingUnit
        {
            Id = Guid.NewGuid(), CreatedAt = now, Version = 1, PackageNumber = number, OutboundId = outbound.Id, Contents = [.. parsed.Contents!],
            WeightKg = parsed.WeightKg, DimensionsCm = parsed.DimensionsCm, CreatedBy = Support.UserId, CreatedByName = Support.ActorName,
            IdempotencyKey = request.IdempotencyKey, RequestFingerprint = fingerprint, CorrelationId = Support.CorrelationId
        };
        var copy = Touched(outbound, now);
        var created = Reads.NewEvent(copy, OutboundEventKind.PackageCreated, now, Data(package));
        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertPackageAsync(package, tx);
                await session.InsertEventsAsync([created], tx);
                return new WarehouseWriteOutcome(1, package.Id);
            }, ct);
        }
        catch (OutboundDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected > 0) return Response<PackageDto>.Success(OutboundShipmentReads.Package(package), 201);
        return await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) ?? WarehouseSupport.Conflict<PackageDto>();
    }

    private async Task<Response<PackageDto>?> CreateReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Outbounds.FindPackageByCreateKeyAsync(Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint != fingerprint ? WarehouseSupport.KeyReused<PackageDto>() : Response<PackageDto>.Success(OutboundShipmentReads.Package(earlier));
    }
}

/// <summary><c>updatePackage</c>: an Open package's content / weight / dimensions (If-Match on the package).</summary>
public sealed class UpdateHandlingUnitHandler : HandlingUnitHandler, IRequestHandler<UpdateHandlingUnitCommand, Response<PackageDto>>
{
    public UpdateHandlingUnitHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support) : base(outbounds, reads, support)
    {
    }

    public async Task<Response<PackageDto>> Handle(UpdateHandlingUnitCommand request, CancellationToken ct)
    {
        var input = request.Package;
        if (!input.HasWeight && !input.HasDimensions && !input.HasContents) return WarehouseSupport.Invalid<PackageDto>("Send at least one of weightKg, dimensionsCm, contents.");
        var (parsed, error) = Parse(input, create: false);
        if (parsed is null) return WarehouseSupport.Invalid<PackageDto>(error!);
        var fingerprint = Fingerprint(WarehouseContract.OpUpdatePackage, request.PackageId.ToString("D"), input, parsed);

        var package = await Outbounds.FindPackageAsync(request.PackageId, ct);
        if (package is null) return NotFound();
        if (await ReplayAsync(package, WarehouseContract.OpUpdatePackage, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        var outbound = await Reads.FindVisibleAsync(package.OutboundId, ct);
        if (outbound is null) return NotFound();
        if (State<PackageDto>(outbound) is { } state) return state;
        if (package.Status != HandlingUnitStatus.Open)
            return WarehouseSupport.Fail<PackageDto>(WarehouseErrorCodes.InvalidTransition, $"A {package.Status} package does not change.", 409);
        if (package.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<PackageDto>();
        if (parsed.Contents is { } contents)
        {
            var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
            var packages = await Outbounds.PackagesAsync(outbound.Id, ct);
            if (Fits<PackageDto>(outbound, OutboundFlowRules.PickedByAllocation(tasks), packages, contents, package.Id) is { } refusal) return refusal;
        }

        var now = Support.Now();
        var p = package.Copy();
        if (input.HasWeight) p.WeightKg = parsed.WeightKg;
        if (input.HasDimensions) p.DimensionsCm = parsed.DimensionsCm;
        if (parsed.Contents is not null) p.Contents = [.. parsed.Contents];
        p.Version = package.Version + 1;
        p.UpdatedAt = now;
        p.UpdatedBy = Support.UserId;
        var copy = Touched(outbound, now);
        var updated = Reads.NewEvent(copy, OutboundEventKind.PackageUpdated, now, Data(p), request.IdempotencyKey, WarehouseContract.OpUpdatePackage, fingerprint);
        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                if (!await session.ReplacePackageAsync(p, package.Version, tx)) throw new OutboundShipments.Handlers.CommandHandlers.OutboundWriteLostException();
                await session.InsertEventsAsync([updated], tx);
                return new WarehouseWriteOutcome(1, p.Id);
            }, ct);
        }
        catch (Exception ex) when (ex is OutboundDuplicateException or OutboundShipments.Handlers.CommandHandlers.OutboundWriteLostException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected > 0) return Response<PackageDto>.Success(OutboundShipmentReads.Package(p));
        return await ReplayAsync(package, WarehouseContract.OpUpdatePackage, request.IdempotencyKey, fingerprint, ct) ?? WarehouseSupport.Conflict<PackageDto>();
    }
}

/// <summary>
/// <c>closePackage</c>: at least one content line (422 WMS_PACKAGE_EMPTY). When the close leaves every picked allocation fully in CLOSED packages, the
/// outbound becomes <c>Packed</c> in the SAME write and every Open package left WITHOUT content is <c>Cancelled</c> (<c>PackageCancelled</c>; r21 F-9).
/// </summary>
public sealed class CloseHandlingUnitHandler : HandlingUnitHandler, IRequestHandler<CloseHandlingUnitCommand, Response<PackageDto>>
{
    public CloseHandlingUnitHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support) : base(outbounds, reads, support)
    {
    }

    public async Task<Response<PackageDto>> Handle(CloseHandlingUnitCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpClosePackage), ("packageId", request.PackageId.ToString("D")));
        var package = await Outbounds.FindPackageAsync(request.PackageId, ct);
        if (package is null) return NotFound();
        if (await ReplayAsync(package, WarehouseContract.OpClosePackage, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        var outbound = await Reads.FindVisibleAsync(package.OutboundId, ct);
        if (outbound is null) return NotFound();
        if (State<PackageDto>(outbound) is { } state) return state;
        if (package.Status != HandlingUnitStatus.Open)
            return WarehouseSupport.Fail<PackageDto>(WarehouseErrorCodes.InvalidTransition, $"A {package.Status} package is not closed again.", 409);
        if (package.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<PackageDto>();
        if (package.Contents.Count == 0) return WarehouseSupport.Fail<PackageDto>(WarehouseErrorCodes.PackageEmpty, "A package without content is not closed.", 422);

        var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
        var packages = await Outbounds.PackagesAsync(outbound.Id, ct);
        var now = Support.Now();
        var p = package.Copy();
        p.Status = HandlingUnitStatus.Closed;
        p.Version = package.Version + 1;
        p.UpdatedAt = now;
        p.UpdatedBy = Support.UserId;

        var after = packages.Select(x => x.Id == p.Id ? p : x).ToList();
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var packed = OutboundFlowRules.PackedByAllocation(after);
        var copy = Touched(outbound, now);
        var events = new List<OutboundShipmentEvent>
            { Reads.NewEvent(copy, OutboundEventKind.PackageClosed, now, Data(p), request.IdempotencyKey, WarehouseContract.OpClosePackage, fingerprint) };
        var cancelled = new List<(HandlingUnit Copy, int Expected)>();
        if (OutboundFlowRules.IsPacked(outbound, picked, packed))
        {
            copy.Status = OutboundStatus.Packed;
            foreach (var empty in after.Where(x => x.Status == HandlingUnitStatus.Open && x.Contents.Count == 0))
            {
                var e = empty.Copy();
                e.Status = HandlingUnitStatus.Cancelled;
                e.Version = empty.Version + 1;
                e.UpdatedAt = now;
                e.UpdatedBy = Support.UserId;
                cancelled.Add((e, empty.Version));
                events.Add(Reads.NewEvent(copy, OutboundEventKind.PackageCancelled, now, new Dictionary<string, string?>
                    { ["packageId"] = e.Id.ToString("D"), ["packageNumber"] = e.PackageNumber, ["reason"] = "EmptyAtPacked" }));
            }

            events.Add(Reads.NewEvent(copy, OutboundEventKind.Packed, now, new Dictionary<string, string?>
            {
                ["packages"] = string.Join(",", after.Where(x => x.Status == HandlingUnitStatus.Closed).Select(x => x.PackageNumber).OrderBy(x => x, StringComparer.Ordinal))
            }));
        }

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                if (!await session.ReplacePackageAsync(p, package.Version, tx)) throw new OutboundShipments.Handlers.CommandHandlers.OutboundWriteLostException();
                foreach (var (e, expected) in cancelled)
                {
                    if (!await session.ReplacePackageAsync(e, expected, tx)) throw new OutboundShipments.Handlers.CommandHandlers.OutboundWriteLostException();
                }

                await session.InsertEventsAsync(events, tx);
                return new WarehouseWriteOutcome(1, p.Id);
            }, ct);
        }
        catch (Exception ex) when (ex is OutboundDuplicateException or OutboundShipments.Handlers.CommandHandlers.OutboundWriteLostException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected > 0) return Response<PackageDto>.Success(OutboundShipmentReads.Package(p));
        return await ReplayAsync(package, WarehouseContract.OpClosePackage, request.IdempotencyKey, fingerprint, ct) ?? WarehouseSupport.Conflict<PackageDto>();
    }
}

/// <summary><c>listPackages</c> — the outbound summary, the allocation summary (picked / packed), the packages; no ship-to (Q-10a).</summary>
public sealed class GetHandlingUnitListHandler : IRequestHandler<GetHandlingUnitListQuery, Response<PackingViewDto>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundShipmentReads _reads;

    public GetHandlingUnitListHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads)
    {
        _outbounds = outbounds;
        _reads = reads;
    }

    public async Task<Response<PackingViewDto>> Handle(GetHandlingUnitListQuery request, CancellationToken ct)
    {
        var outbound = await _reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<PackingViewDto>();
        var tasks = await _outbounds.PickTasksAsync(outbound.Id, ct);
        var packages = await _outbounds.PackagesAsync(outbound.Id, ct);
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var packed = OutboundFlowRules.PackedByAllocation(packages);
        return Response<PackingViewDto>.Success(new PackingViewDto(
            new PackingOutboundDto(outbound.Id.ToString("D"), outbound.OutboundNumber, outbound.Status.ToString(), outbound.Version),
            outbound.Lines.SelectMany(l => l.Allocations.Select(a => new PackingAllocationDto(a.AllocSeq,
                new OutboundItemDto(l.ItemId.ToString("D"), l.SkuId.ToString("D"), WireNames.ToWire(l.SkuLevel)), a.LotId, a.LotNumber,
                InventoryQuantity.ToWire(picked.GetValueOrDefault(a.AllocSeq)), InventoryQuantity.ToWire(packed.GetValueOrDefault(a.AllocSeq))))).ToList(),
            packages.Select(OutboundShipmentReads.Package).ToList()));
    }
}
