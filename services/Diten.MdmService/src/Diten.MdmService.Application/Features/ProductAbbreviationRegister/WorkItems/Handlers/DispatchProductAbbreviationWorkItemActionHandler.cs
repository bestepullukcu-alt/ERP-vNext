using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;

public sealed class DispatchProductAbbreviationWorkItemActionHandler
    : IRequestHandler<DispatchProductAbbreviationWorkItemActionCommand,
        ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>>
{
    private readonly IMediator _mediator;
    private readonly IProductAbbreviationRegisterRepository _register;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public DispatchProductAbbreviationWorkItemActionHandler(
        IMediator mediator,
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _register = register;
        _globalProducts = globalProducts;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>> Handle(
        DispatchProductAbbreviationWorkItemActionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.HasUnmappedFields
            || request.ItemId == Guid.Empty
            || !string.Equals(
                request.ProviderCode,
                ProductAbbreviationWorkItemContract.ProviderCode,
                StringComparison.Ordinal)
            || !ProductAbbreviationWorkItemContract.ActionCodes.Contains(request.ActionCode)
            || request.ExpectedVersion is null or < 0)
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }

        var reason = NormalizeReason(request.Reason ?? request.Note);
        if (request.ActionCode == "reject" && string.IsNullOrWhiteSpace(reason))
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }
        if (reason?.Length > 512)
        {
            return Fail(400, "WORK_ITEM_ACTION_PAYLOAD_INVALID");
        }

        var entry = await _register.GetByIdAsync(request.ItemId, cancellationToken);
        if (entry is null)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }
        if (entry.ReplacesEntryId is not null)
        {
            return Fail(409, "CONCURRENCY_CONFLICT");
        }

        var scope = await _scopeGuard.ResolveContextAsync(PermissionFor(request.ActionCode), cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Fail(scope.StatusCode, scope.FailureCode!);
        }
        if (scope.Context!.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced
            && await _globalProducts.GetByIdAsync(entry.GlobalProductId, cancellationToken) is null)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }
        var decision = await _scopeGuard.EvaluateAsync(scope.Context, entry.GlobalProductId, cancellationToken);
        if (!decision.Allowed)
        {
            return Fail(404, "ABBREVIATION_NOT_FOUND");
        }

        var operationKey = BuildOperationKey(
            request.ItemId,
            request.ActionCode,
            request.ExpectedVersion.Value,
            reason);
        Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto> response = request.ActionCode switch
        {
            "approve" => await _mediator.Send(
                new ApproveProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    ExpectedFormerVersion: null,
                    reason),
                cancellationToken),
            "reject" => await _mediator.Send(
                new RejectProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    reason!),
                cancellationToken),
            "cancel" => await _mediator.Send(
                new CancelProductAbbreviationAllocationCommand(
                    request.ItemId,
                    request.ExpectedVersion.Value,
                    operationKey,
                    reason),
                cancellationToken),
            _ => throw new InvalidOperationException("Validated action code was not dispatchable.")
        };

        if (!response.IsSuccessful)
        {
            return Fail(
                response.StatusCode,
                response.Errors.FirstOrDefault() ?? "WORK_ITEM_ACTION_FAILED");
        }

        return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Success(
            new(
                request.ItemId.ToString("D"),
                ProductAbbreviationWorkItemContract.ProviderCode,
                request.ActionCode));
    }

    public static string BuildOperationKey(
        Guid itemId,
        string actionCode,
        int expectedVersion,
        string? reason)
    {
        reason = NormalizeReason(reason);
        var canonicalReason = reason is null
            ? "null"
            : $"value:{Encoding.UTF8.GetByteCount(reason).ToString(CultureInfo.InvariantCulture)}:{reason}";
        var canonicalPayload = string.Join(
            '\n',
            $"actionCode={actionCode}",
            $"expectedVersion={expectedVersion.ToString(CultureInfo.InvariantCulture)}",
            $"reason={canonicalReason}",
            string.Empty);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
        return $"abb-wc:{itemId:N}:{actionCode}:v{expectedVersion.ToString(CultureInfo.InvariantCulture)}:{payloadHash}";
    }

    private static string? NormalizeReason(string? reason)
        => reason?.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static string PermissionFor(string actionCode) => actionCode switch
    {
        "approve" => ProductAbbreviationPermissions.Approve,
        "reject" => ProductAbbreviationPermissions.Reject,
        "cancel" => ProductAbbreviationPermissions.Cancel,
        _ => throw new InvalidOperationException("Validated action code has no permission mapping.")
    };

    private static ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse> Fail(
        int statusCode,
        string reasonCode)
        => ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>.Fail(
            statusCode,
            reasonCode);
}
