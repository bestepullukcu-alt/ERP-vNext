using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class RequestPayrollIntegrationReplayHandler : IRequestHandler<RequestPayrollIntegrationReplayCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public RequestPayrollIntegrationReplayHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RequestPayrollIntegrationReplayCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var idempotencyKey = request.Request.IdempotencyKey.Trim();
        if (await _repository.ExistsActiveReplayIdempotencyKeyAsync(run.Id, idempotencyKey, ct))
        {
            return Response<Guid>.Fail("A replay request with this idempotency key already exists.", 409);
        }

        var replay = new PayrollIntegrationRetryReplayRequest
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            RequestType = request.Request.RequestType,
            RequestedByActorId = request.Request.RequestedByActorId,
            PurposeCode = request.Request.PurposeCode.Trim(),
            IdempotencyKey = idempotencyKey,
            ApprovalWorkflowId = request.Request.ApprovalWorkflowId,
            RequestState = request.Request.RequestState,
            RedactedReason = request.Request.RedactedReason.Trim()
        };
        await _repository.CreateRetryReplayRequestAsync(replay, ct);
        return Response<Guid>.Success(replay.Id, 201);
    }
}
