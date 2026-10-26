using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/// <summary>
/// MOD-0290-FU05 slice 2 — an approved Global Product's brand (owner Karar a; CT decisions 2026-10-15):
/// <list type="bullet">
/// <item>only an approved product with no lifecycle operation (pending / retired / correction or retirement running → 409);</item>
/// <item>a NEW brand passes FU02's link rule (<see cref="GlobalProductBrands"/>); null removes the brand;</item>
/// <item>the same brand → 200, nothing written, the version stays;</item>
/// <item>the intent (GlobalProductBrandChanged) carries the brand before and after; "before" is the brand at the version
/// the write is conditioned on, recorded in the same write (<see cref="GlobalProductBrandChange"/>), so an exact repeat
/// rebuilds the same intent (replay) and another brand under the same key is a conflict.</item>
/// </list>
/// </summary>
public sealed class ChangeGlobalProductBrandHandler
    : IRequestHandler<ChangeGlobalProductBrandCommand, Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>>
{
    public const string Permission = ProductIdentityLifecyclePermissions.GlobalProductUpdateBrand;

    private readonly IGlobalProductRepository _products;
    private readonly IProductIdentityLifecycleActorContext _actorContext;
    private readonly TimeProvider _clock;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IBrandRepository? _brands;

    public ChangeGlobalProductBrandHandler(
        IGlobalProductRepository products,
        IProductIdentityLifecycleActorContext actorContext,
        TimeProvider clock,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository scopePolicies,
        ProductLegalEntityScopeCandidateFacade scopeCandidates,
        ITenantContext tenantContext,
        IBrandRepository? brands = null)
    {
        _products = products;
        _actorContext = actorContext;
        _clock = clock;
        _scopeGuard = new(rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        _brands = brands;
    }

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>> Handle(
        ChangeGlobalProductBrandCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_actorContext.TryResolveCanonicalHumanSubject(out var actorId)
            || !_actorContext.HasPermission(Permission))
        {
            return Fail("GLOBAL_PRODUCT_BRAND_CHANGE_FORBIDDEN", 403);
        }

        var input = request.Request;
        if (input is null || request.GlobalProductId == Guid.Empty || request.OperationId == Guid.Empty
            || input.ExpectedVersion is null || input.ExpectedVersion < 0
            || !input.BrandIdSpecified || input.BrandId == Guid.Empty)
        {
            return Fail("GLOBAL_PRODUCT_BRAND_CHANGE_REQUEST_INVALID", 400);
        }

        var scope = await _scopeGuard.ResolveContextAsync(Permission, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }

        var product = await _products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        if (product is null || !(await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken)).Allowed)
        {
            return Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        }

        var expectedVersion = input.ExpectedVersion.Value;
        var operationKey = request.OperationId.ToString("D");
        var requested = input.BrandId;
        // A change this key already wrote: its "before" is the recorded one (the product shows the "after" now).
        var recorded = product.BrandChanges?.SingleOrDefault(change =>
            string.Equals(change.OperationKey, operationKey, StringComparison.Ordinal));
        if (recorded is null)
        {
            if (product.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
                || product.ActiveLifecycleOperation is not null)
            {
                return Fail("GLOBAL_PRODUCT_STATE_CONFLICT", 409);
            }

            // CT decision: the same brand is no change — 200, nothing written, the version stays.
            if (requested == product.BrandId)
            {
                return await SuccessAsync(product, changed: false, isReplay: false, cancellationToken);
            }

            if (requested is { } newBrand)
            {
                var link = await GlobalProductBrands.ResolveLinkAsync(_brands, newBrand, _clock.GetUtcNow(), cancellationToken);
                if (link.Refusal is { } refusal)
                {
                    return BrandProductFailures.Fail<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>(
                        refusal.ReasonCode, refusal.Message, refusal.StatusCode);
                }
            }
        }

        var change = new GlobalProductBrandChange
        {
            OperationKey = operationKey,
            PreVersion = expectedVersion,
            FromBrandId = recorded?.FromBrandId ?? product.BrandId,
            ToBrandId = requested
        };
        var auditIntent = CreateAuditIntent(product, expectedVersion, operationKey, actorId, change, _clock.GetUtcNow());
        var result = await _products.ChangeBrandAsync(product.Id, expectedVersion, change, auditIntent, cancellationToken);
        if (!result.Succeeded || result.GlobalProduct is null)
        {
            return Fail(result.ErrorCode ?? "GLOBAL_PRODUCT_STATE_CONFLICT", StatusFor(result.ErrorCode));
        }

        return await SuccessAsync(result.GlobalProduct, changed: true, result.IsReplay, cancellationToken);
    }

    private async Task<Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>> SuccessAsync(
        GlobalProduct product,
        bool changed,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var brand = GlobalProductBrands.Describe(
            product.BrandId,
            await GlobalProductBrands.ReadAsync(_brands, [product.BrandId], cancellationToken));
        return Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>.Success(new(
            product.Id,
            product.CanonicalCode,
            product.GlobalProductName,
            product.LifecycleStatus,
            product.Version,
            changed,
            isReplay,
            brand.Id,
            brand.Code,
            brand.Name));
    }

    private static LocalAuditIntent CreateAuditIntent(
        GlobalProduct product,
        int expectedVersion,
        string operationKey,
        Guid actorId,
        GlobalProductBrandChange change,
        DateTimeOffset timestampUtc)
    {
        var evidence = EncodeFacts(
            product.TenantId.ToString("D"),
            product.Id.ToString("D"),
            actorId.ToString("D"),
            expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            change.FromBrandId is { } from ? $"FROM:{from:D}" : "FROM:NONE",
            change.ToBrandId is { } to ? $"TO:{to:D}" : "TO:NONE");
        var intentId = DeterministicGuid(
            $"{product.TenantId:D}|{product.Id:D}|{ProductAuditOperation.GlobalProductBrandChanged}|{operationKey}");
        var postVersion = checked(expectedVersion + 1);
        return new LocalAuditIntent
        {
            IntentId = intentId,
            TenantId = product.TenantId,
            AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = product.Id,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = ProductAuditOperation.GlobalProductBrandChanged,
            ActorId = actorId.ToString("D"),
            CorrelationId = operationKey,
            CausationId = operationKey,
            CommandId = operationKey,
            Sequence = postVersion,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"GlobalProduct/{product.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = operationKey
        };
    }

    private static byte[] EncodeFacts(params string[] facts)
    {
        using var stream = new MemoryStream();
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        foreach (var fact in facts)
        {
            var bytes = Encoding.UTF8.GetBytes(fact);
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, bytes.Length);
            stream.Write(lengthBytes);
            stream.Write(bytes);
        }
        return stream.ToArray();
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

    private static int StatusFor(string? code) => code switch
    {
        "GLOBAL_PRODUCT_NOT_FOUND" => 404,
        "AUDIT_INTENT_CONTRACT_INVALID" or "GLOBAL_PRODUCT_BRAND_CHANGE_CONTRACT_INVALID" => 500,
        _ => 409
    };

    private static Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto> Fail(string code, int statusCode) =>
        Response<ProductItemSkuMasterModels.GlobalProductBrandChangeDto>.Fail(code, statusCode);
}
