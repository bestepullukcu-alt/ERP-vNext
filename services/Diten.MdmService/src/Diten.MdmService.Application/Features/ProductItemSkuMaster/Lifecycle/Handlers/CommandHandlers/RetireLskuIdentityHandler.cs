using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class RetireLskuIdentityHandler(
    ILskuRepository lskus,
    IProductIdentityLifecycleActorContext actorContext,
    TimeProvider clock)
    : IRequestHandler<RetireLskuIdentityCommand, Response<LskuIdentityLifecycleResult>>
{
    public async Task<Response<LskuIdentityLifecycleResult>> Handle(
        RetireLskuIdentityCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = command.Request;
        if (!actorContext.TryResolveCanonicalHumanSubject(out var actor)
            || !actorContext.HasPermission(LskuIdentityLifecyclePermissions.Retire))
            return Fail("LSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        if (input is null || input.LskuId == Guid.Empty || input.ExpectedVersion < 0
            || input.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(input.ReasonCode))
            return Fail("LSKU_IDENTITY_RETIRE_REQUEST_INVALID", 400);

        var lsku = await lskus.GetByIdAsync(input.LskuId, cancellationToken);
        if (lsku is null) return Fail("LSKU_IDENTITY_NOT_FOUND", 404);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetire(
            lsku, input.ExpectedVersion, input.OperationId, actor,
            input.ReasonCode, input.Comment, clock.GetUtcNow());
        var result = await lskus.RetireIdentityAsync(
            input.LskuId, input.ExpectedVersion, audit, cancellationToken);
        if (!result.Succeeded || result.Lsku is null)
            return Fail(result.ErrorCode ?? "LSKU_IDENTITY_STATE_CONFLICT", StatusFor(result.ErrorCode));
        var updated = result.Lsku;
        return Response<LskuIdentityLifecycleResult>.Success(new(
            updated.Id, updated.LifecycleStatus, updated.Version,
            updated.IdentityWorkflowBinding?.WorkflowInstanceId, result.IsReplay));
    }

    private static int StatusFor(string? code) => code switch
    {
        "LSKU_IDENTITY_NOT_FOUND" => 404,
        "AUDIT_INTENT_CAPACITY_EXCEEDED" => 503,
        _ => 409
    };

    private static Response<LskuIdentityLifecycleResult> Fail(string code, int status) =>
        Response<LskuIdentityLifecycleResult>.Fail(code, status);
}
