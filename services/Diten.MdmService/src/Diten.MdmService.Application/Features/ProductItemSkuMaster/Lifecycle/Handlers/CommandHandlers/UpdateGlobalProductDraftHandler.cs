using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
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

public sealed class UpdateGlobalProductDraftHandler
    : IRequestHandler<UpdateGlobalProductDraftCommand, Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto>>
{
    public const string Permission = "mdm.global-products.update";

    private readonly IGlobalProductRepository _products;
    private readonly IProductIdentityLifecycleActorContext _actorContext;
    private readonly TimeProvider _clock;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public UpdateGlobalProductDraftHandler(
        IGlobalProductRepository products,
        IProductIdentityLifecycleActorContext actorContext,
        TimeProvider clock,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository scopePolicies,
        ProductLegalEntityScopeCandidateFacade scopeCandidates,
        ITenantContext tenantContext)
    {
        _products = products;
        _actorContext = actorContext;
        _clock = clock;
        _scopeGuard = new(rolloutStates, scopePolicies, scopeCandidates, tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto>> Handle(
        UpdateGlobalProductDraftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_actorContext.TryResolveCanonicalHumanSubject(out var actorId)
            || !_actorContext.HasPermission(Permission))
        {
            return Fail("GLOBAL_PRODUCT_UPDATE_FORBIDDEN", 403);
        }

        var input = request.Request;
        if (input is null || request.GlobalProductId == Guid.Empty || request.OperationId == Guid.Empty
            || input.ExpectedVersion is null || input.ExpectedVersion < 0
            || !GlobalProductNameRules.HasValidLength(input.GlobalProductName))
        {
            return Fail("GLOBAL_PRODUCT_UPDATE_REQUEST_INVALID", 400);
        }

        var scope = await _scopeGuard.ResolveContextAsync(Permission, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.FailureCode!, scope.StatusCode);
        }

        var product = await _products.GetByIdAsync(request.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed)
        {
            return Fail("GLOBAL_PRODUCT_NOT_FOUND", 404);
        }

        var visibleName = GlobalProductNameRules.CleanVisible(input.GlobalProductName!);
        var normalizedName = GlobalProductNameRules.NormalizeDuplicateKey(visibleName);
        var auditIntent = CreateAuditIntent(
            product,
            input.ExpectedVersion.Value,
            request.OperationId,
            actorId,
            visibleName,
            normalizedName,
            _clock.GetUtcNow());
        var result = await _products.UpdateDraftAsync(
            product.Id,
            visibleName,
            normalizedName,
            input.ExpectedVersion.Value,
            auditIntent,
            cancellationToken);
        if (!result.Succeeded || result.GlobalProduct is null)
        {
            return Fail(
                result.ErrorCode ?? "GLOBAL_PRODUCT_STATE_CONFLICT",
                StatusFor(result.ErrorCode));
        }

        var updated = result.GlobalProduct;
        return Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto>.Success(new(
            updated.Id,
            updated.CanonicalCode,
            updated.GlobalProductName,
            updated.LifecycleStatus,
            updated.Version,
            result.IsReplay));
    }

    private static LocalAuditIntent CreateAuditIntent(
        GlobalProduct product,
        int expectedVersion,
        Guid operationId,
        Guid actorId,
        string visibleName,
        string normalizedName,
        DateTimeOffset timestampUtc)
    {
        var operationKey = operationId.ToString("D");
        var evidence = EncodeFacts(
            product.TenantId.ToString("D"),
            product.Id.ToString("D"),
            actorId.ToString("D"),
            expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            visibleName,
            normalizedName);
        var intentId = DeterministicGuid(
            $"{product.TenantId:D}|{product.Id:D}|{ProductAuditOperation.GlobalProductDraftUpdated}|{operationKey}");
        var postVersion = checked(expectedVersion + 1);
        return new LocalAuditIntent
        {
            IntentId = intentId,
            TenantId = product.TenantId,
            AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = product.Id,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = ProductAuditOperation.GlobalProductDraftUpdated,
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
        "AUDIT_INTENT_CONTRACT_INVALID" or "GLOBAL_PRODUCT_UPDATE_CONTRACT_INVALID" => 500,
        _ => 409
    };

    private static Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto> Fail(
        string code,
        int statusCode) =>
        Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto>.Fail(code, statusCode);
}
