using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/*
 * LSKU FIX1 B1 (c): before FIX1 every LSKU creation that failed after its admission left the admission on the parent
 * GSKU for good (the GSKU could then not retire or be corrected, and after 32 could not take a child). This finds the
 * admissions older than the cutoff whose LSKU row never came, releases each and keeps an audit intent on the GSKU.
 * A row with the admission's command — even a deleted one — means the creation reached the store: it is left alone.
 * The cutoff keeps a creation still in flight out of reach. A repeat finds nothing new; a released one replays.
 */
public sealed class RepairLskuChildAdmissionsHandler
    : IRequestHandler<RepairLskuChildAdmissionsCommand, Response<LskuChildAdmissionRepairResult>>
{
    public const int MinimumAgeFloorMinutes = 5;
    public const int MaximumLimit = 500;
    private readonly IGskuRepository _gskus;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly TimeProvider _time;

    public RepairLskuChildAdmissionsHandler(
        IGskuRepository gskus,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext,
        TimeProvider time)
    {
        _gskus = gskus;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _time = time;
    }

    public async Task<Response<LskuChildAdmissionRepairResult>> Handle(
        RepairLskuChildAdmissionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_tenantContext.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(_actorContext.ActorId))
        {
            return Response<LskuChildAdmissionRepairResult>.Fail("TENANT_CONTEXT_REQUIRED", 403);
        }

        if (request.Limit is < 1 or > MaximumLimit || request.MinimumAgeMinutes < MinimumAgeFloorMinutes)
        {
            return Response<LskuChildAdmissionRepairResult>.Fail("LSKU_ADMISSION_REPAIR_REQUEST_INVALID", 400);
        }

        var now = _time.GetUtcNow().ToUniversalTime();
        var orphans = await _gskus.FindOrphanChildCreationAdmissionsAsync(
            GskuChildIdentityKind.Lsku, now.AddMinutes(-request.MinimumAgeMinutes), request.Limit, cancellationToken);
        var items = new List<LskuChildAdmissionRepairItem>(orphans.Count);
        // Each release moves the GSKU's version, so a second orphan on the same GSKU is re-read before its write.
        var versions = new Dictionary<Guid, int>();
        foreach (var orphan in orphans)
        {
            var current = versions.TryGetValue(orphan.GskuId, out var version) ? orphan with { GskuVersion = version } : orphan;
            var result = await _gskus.RepairOrphanChildCreationAdmissionAsync(
                current, CreateIntent(current, now), cancellationToken);
            if (result.Gsku is not null)
            {
                versions[orphan.GskuId] = result.Gsku.Version;
            }

            items.Add(new(orphan.GskuId, orphan.CreationCommandId, orphan.AcquiredAtUtc,
                result.Succeeded, result.Succeeded ? null : result.ErrorCode));
        }

        return Response<LskuChildAdmissionRepairResult>.Success(
            new(orphans.Count, items.Count(x => x.Released), items), 200);
    }

    private LocalAuditIntent CreateIntent(GskuOrphanChildAdmission orphan, DateTimeOffset now)
    {
        var tenantId = _tenantContext.TenantId;
        var facts = $"{tenantId:D}|{orphan.GskuId:D}|{(int)orphan.ChildKind}|{orphan.CreationCommandId}|{orphan.RequestFingerprint}|{orphan.AcquiredAtUtc:O}";
        var key = "admission-repair:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tenantId:D}|{orphan.GskuId:D}|{(int)orphan.ChildKind}|{orphan.CreationCommandId}"))).ToLowerInvariant();
        var postVersion = checked(orphan.GskuVersion + 1);
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid($"{tenantId:D}|{orphan.GskuId:D}|{ProductAuditOperation.GskuChildCreationAdmissionReleased}|{key}"),
            TenantId = tenantId,
            AggregateType = AuditAggregateType.Gsku,
            AggregateId = orphan.GskuId,
            PreVersion = orphan.GskuVersion,
            PostVersion = postVersion,
            Operation = ProductAuditOperation.GskuChildCreationAdmissionReleased,
            ActorId = _actorContext.ActorId,
            CorrelationId = key,
            CausationId = orphan.CreationCommandId,
            CommandId = key,
            Sequence = postVersion,
            TimestampUtc = now,
            TimestampUtcTicksV1 = now.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))),
            SnapshotReference = $"Gsku/{orphan.GskuId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = key
        };
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
