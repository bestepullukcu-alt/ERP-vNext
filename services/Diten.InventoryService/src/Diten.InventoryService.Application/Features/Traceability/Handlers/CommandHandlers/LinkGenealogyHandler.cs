using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Commands;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// linkGenealogy (proposal §4.7; writer MOD-0194 later, API-only permission <c>trace.genealogy.link</c>). Order:
/// <list type="number">
/// <item>Shape (400): key, the two lot ids, path lot = <c>childLotId</c>, reference 1–60, source module 1–40, quantity a
/// decimal string.</item>
/// <item>Self link → 422 <c>GENEALOGY_SELF_LINK</c>. A committed key → replay (same fingerprint) or 400.</item>
/// <item>One transaction: both lots in the tenant (404 <c>UNKNOWN_LOT</c>); the same parent + child + reference already
/// linked → 200 replay; the child already an ancestor of the parent → 422 <c>GENEALOGY_CYCLE</c>; insert + one
/// <c>Linked</c> history row on the child lot.</item>
/// </list>
/// </summary>
public sealed partial class LinkGenealogyHandler : IRequestHandler<LinkGenealogyCommand, Response<GenealogyLinkDto>>
{
    /// <summary>Upper bound of the ancestor walk — far above any real bill of materials depth; reaching it is refused.</summary>
    public const int MaxAncestorWalk = 5000;

    private readonly ILotRepository _lots;
    private readonly TraceWriteSupport _support;

    public LinkGenealogyHandler(ILotRepository lots, TraceWriteSupport support)
    {
        _lots = lots;
        _support = support;
    }

    public async Task<Response<GenealogyLinkDto>> Handle(LinkGenealogyCommand request, CancellationToken cancellationToken)
    {
        var keyError = TraceWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return TraceWriteSupport.Invalid<GenealogyLinkDto>(keyError);
        if (!TraceWriteSupport.TryParseId(request.ParentLotId, out var parentId) || !TraceWriteSupport.TryParseId(request.ChildLotId, out var childId))
            return TraceWriteSupport.Invalid<GenealogyLinkDto>("parentLotId and childLotId must be lot ids.");
        if (!string.Equals(request.LotId, request.ChildLotId, StringComparison.Ordinal))
            return TraceWriteSupport.Invalid<GenealogyLinkDto>("The lot in the path must be childLotId.");

        var reference = TraceWriteSupport.Trimmed(request.TransformationReference);
        var sourceModule = TraceWriteSupport.Trimmed(request.SourceModule);
        var uom = TraceWriteSupport.Trimmed(request.UomId);
        if (reference is null || reference.Length > TraceContract.MaxTransformationReferenceLength)
            return TraceWriteSupport.Invalid<GenealogyLinkDto>($"transformationReference is required: 1-{TraceContract.MaxTransformationReferenceLength} characters.");
        if (sourceModule is null || sourceModule.Length > TraceContract.MaxSourceModuleLength)
            return TraceWriteSupport.Invalid<GenealogyLinkDto>($"sourceModule is required: 1-{TraceContract.MaxSourceModuleLength} characters.");
        if (request.QuantityConsumed is not null && !DecimalString().IsMatch(request.QuantityConsumed))
            return TraceWriteSupport.Invalid<GenealogyLinkDto>("quantityConsumed must be a decimal string (up to 6 fraction digits).");
        if (uom is not null && uom.Length > TraceContract.MaxAttributeLength)
            return TraceWriteSupport.Invalid<GenealogyLinkDto>($"uomId must be at most {TraceContract.MaxAttributeLength} characters.");

        if (parentId == childId)
            return TraceWriteSupport.Fail<GenealogyLinkDto>(TraceErrorCodes.GenealogySelfLink, "A lot cannot be its own parent.", 422);

        var fingerprint = TraceWriteSupport.Fingerprint(
            ("parentLotId", TraceMapping.Id(parentId)), ("childLotId", TraceMapping.Id(childId)), ("transformationReference", reference),
            ("sourceModule", sourceModule), ("quantityConsumed", request.QuantityConsumed), ("uomId", uom));

        if (await _lots.FindLinkByKeyAsync(key, cancellationToken) is { } committed) return Replay(committed, fingerprint);

        try
        {
            return await _lots.InTransactionAsync(async (tx, ct) =>
            {
                var parent = await tx.FindAsync(parentId, ct);
                var child = await tx.FindAsync(childId, ct);
                if (parent is null || child is null) return TraceWriteSupport.UnknownLot<GenealogyLinkDto>();

                if (await tx.FindLinkAsync(parentId, childId, reference, ct) is { } existing)
                    return Response<GenealogyLinkDto>.Success(TraceMapping.ToLink(existing), 200);

                if (await IsAncestorAsync(tx, candidate: childId, of: parentId, ct))
                    return TraceWriteSupport.Fail<GenealogyLinkDto>(TraceErrorCodes.GenealogyCycle,
                        "The child lot is already an ancestor of the parent lot; the link would close a cycle.", 422);

                var now = DateTimeOffset.UtcNow;
                var link = new LotGenealogyLink
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    ParentLotId = parentId,
                    ChildLotId = childId,
                    TransformationReference = reference,
                    SourceModule = sourceModule,
                    QuantityConsumed = request.QuantityConsumed,
                    UomId = uom,
                    CreatedBy = _support.UserId,
                    IdempotencyKey = key,
                    RequestFingerprint = fingerprint
                };
                await tx.InsertLinkAsync(link, ct);
                await tx.AppendHistoryAsync([_support.History(childId, LotChangeAction.Linked, "genealogy.parent", null,
                    $"{parent.LotNumber} · {reference} · {sourceModule}", null, now)], ct);
                return Response<GenealogyLinkDto>.Success(TraceMapping.ToLink(link), 201);
            }, cancellationToken);
        }
        catch (LotUniqueViolationException violation) when (violation.Index is LotUniqueIndex.GenealogyKey or LotUniqueIndex.GenealogyLink)
        {
            if (await _lots.FindLinkByKeyAsync(key, cancellationToken) is { } winner) return Replay(winner, fingerprint);
            var same = (await _lots.ChildLinksAsync([parentId], cancellationToken))
                .FirstOrDefault(l => l.ChildLotId == childId && l.TransformationReference == reference)
                ?? throw new InvalidOperationException("Genealogy link reported taken but not found.");
            return Response<GenealogyLinkDto>.Success(TraceMapping.ToLink(same), 200);
        }
    }

    /// <summary>Walks the parents of <paramref name="of"/> upwards: is <paramref name="candidate"/> among its ancestors?</summary>
    private static async Task<bool> IsAncestorAsync(ILotTransaction tx, Guid candidate, Guid of, CancellationToken ct)
    {
        var seen = new HashSet<Guid> { of };
        var frontier = new List<Guid> { of };
        while (frontier.Count > 0)
        {
            var parents = (await tx.ParentLinksAsync(frontier, ct)).Select(l => l.ParentLotId).Where(seen.Add).ToList();
            if (parents.Contains(candidate)) return true;
            if (seen.Count > MaxAncestorWalk)
                throw new InvalidOperationException($"Genealogy ancestor walk exceeded {MaxAncestorWalk} lots.");
            frontier = parents;
        }

        return false;
    }

    private static Response<GenealogyLinkDto> Replay(LotGenealogyLink committed, string fingerprint)
        => string.Equals(committed.RequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<GenealogyLinkDto>.Success(TraceMapping.ToLink(committed), 200)
            : TraceWriteSupport.Fail<GenealogyLinkDto>(TraceErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different genealogy link.", 400);

    [GeneratedRegex(@"^\d{1,18}(\.\d{1,6})?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalString();
}
