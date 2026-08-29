using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class RetireGskuIdentityPairHandler(
    FirstGskuIdentityRetirementProcessor processor,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IProductIdentityLifecycleActorContext actorContext)
    : IRequestHandler<RetireGskuIdentityPairCommand, Response<GskuPairRetirementResult>>
{
    public async Task<Response<GskuPairRetirementResult>> Handle(
        RetireGskuIdentityPairCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var actorId)
            || !actorContext.HasPermission(GskuPairRetirementPermissions.Retire))
        {
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }
        var input = request.Request;
        if (input is null)
        {
            return Fail("FIRST_GSKU_RETIREMENT_REQUEST_INVALID", 400);
        }
        var result = await processor.StartAsync(
            input.GskuId,
            input.ExpectedGskuVersion,
            input.OperationId,
            actorId,
            input.ReasonCode,
            $"interactive:{actorId:D}",
            TimeSpan.FromMinutes(1),
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
        {
            return Fail(result.ErrorCode ?? "FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED", result.StatusCode);
        }

        var operation = result.Operation;
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (gsku is null || revision is null)
        {
            return Fail("FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED", 409);
        }
        return Response<GskuPairRetirementResult>.Success(new(
            revision.Id,
            gsku.Id,
            revision.LifecycleStatus,
            gsku.LifecycleStatus,
            revision.Version,
            gsku.Version,
            result.IsReplay));
    }

    private static Response<GskuPairRetirementResult> Fail(string code, int statusCode) =>
        Response<GskuPairRetirementResult>.Fail(code, statusCode);
}
