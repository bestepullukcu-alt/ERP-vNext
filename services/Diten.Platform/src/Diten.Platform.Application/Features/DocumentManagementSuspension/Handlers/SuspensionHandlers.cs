using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Commands;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Queries;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Handlers;

// MOD-0029-FU13 — thin MediatR handlers delegating to the suspension / retirement / temporary-instruction services.

public sealed class OpenSuspensionCaseHandler(DocumentSuspensionService s)
    : IRequestHandler<OpenSuspensionCaseCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(OpenSuspensionCaseCommand r, CancellationToken ct) =>
        s.OpenAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class EscalateSuspensionCaseHandler(DocumentSuspensionService s)
    : IRequestHandler<EscalateSuspensionCaseCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(EscalateSuspensionCaseCommand r, CancellationToken ct) =>
        s.EscalateAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class ApproveSuspensionHandler(DocumentSuspensionService s)
    : IRequestHandler<ApproveSuspensionCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(ApproveSuspensionCommand r, CancellationToken ct) =>
        s.ApproveAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class RejectSuspensionHandler(DocumentSuspensionService s)
    : IRequestHandler<RejectSuspensionCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(RejectSuspensionCommand r, CancellationToken ct) =>
        s.RejectAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class ExecuteSuspensionHandler(DocumentSuspensionService s)
    : IRequestHandler<ExecuteSuspensionCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(ExecuteSuspensionCommand r, CancellationToken ct) =>
        s.ExecuteAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class CloseSuspensionCaseHandler(DocumentSuspensionService s)
    : IRequestHandler<CloseSuspensionCaseCommand, Response<SuspensionCaseModel>>
{
    public Task<Response<SuspensionCaseModel>> Handle(CloseSuspensionCaseCommand r, CancellationToken ct) =>
        s.CloseAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class GetSuspensionCasesHandler(DocumentSuspensionService s)
    : IRequestHandler<GetSuspensionCasesQuery, Response<IReadOnlyList<SuspensionCaseModel>>>
{
    public Task<Response<IReadOnlyList<SuspensionCaseModel>>> Handle(GetSuspensionCasesQuery r, CancellationToken ct) =>
        s.ListAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class RequestRetirementHandler(DocumentRetirementService s)
    : IRequestHandler<RequestRetirementCommand, Response<RetirementCaseModel>>
{
    public Task<Response<RetirementCaseModel>> Handle(RequestRetirementCommand r, CancellationToken ct) =>
        s.RequestAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class ApproveRetirementHandler(DocumentRetirementService s)
    : IRequestHandler<ApproveRetirementCommand, Response<RetirementCaseModel>>
{
    public Task<Response<RetirementCaseModel>> Handle(ApproveRetirementCommand r, CancellationToken ct) =>
        s.ApproveAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class RejectRetirementHandler(DocumentRetirementService s)
    : IRequestHandler<RejectRetirementCommand, Response<RetirementCaseModel>>
{
    public Task<Response<RetirementCaseModel>> Handle(RejectRetirementCommand r, CancellationToken ct) =>
        s.RejectAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class ExecuteRetirementHandler(DocumentRetirementService s)
    : IRequestHandler<ExecuteRetirementCommand, Response<RetirementCaseModel>>
{
    public Task<Response<RetirementCaseModel>> Handle(ExecuteRetirementCommand r, CancellationToken ct) =>
        s.ExecuteAsync(r.RegisterEntryId, r.CaseId, r.Input, r.CorrelationId, ct);
}

public sealed class GetRetirementCasesHandler(DocumentRetirementService s)
    : IRequestHandler<GetRetirementCasesQuery, Response<IReadOnlyList<RetirementCaseModel>>>
{
    public Task<Response<IReadOnlyList<RetirementCaseModel>>> Handle(GetRetirementCasesQuery r, CancellationToken ct) =>
        s.ListAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class StartTemporaryInstructionControlHandler(TemporaryInstructionService s)
    : IRequestHandler<StartTemporaryInstructionControlCommand, Response<TemporaryInstructionModel>>
{
    public Task<Response<TemporaryInstructionModel>> Handle(StartTemporaryInstructionControlCommand r, CancellationToken ct) =>
        s.StartAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class EvaluateTemporaryInstructionExpiryHandler(TemporaryInstructionService s)
    : IRequestHandler<EvaluateTemporaryInstructionExpiryCommand, Response<TemporaryInstructionModel>>
{
    public Task<Response<TemporaryInstructionModel>> Handle(EvaluateTemporaryInstructionExpiryCommand r, CancellationToken ct) =>
        s.EvaluateExpiryAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class CloseTemporaryInstructionHandler(TemporaryInstructionService s)
    : IRequestHandler<CloseTemporaryInstructionCommand, Response<TemporaryInstructionModel>>
{
    public Task<Response<TemporaryInstructionModel>> Handle(CloseTemporaryInstructionCommand r, CancellationToken ct) =>
        s.CloseAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class GetTemporaryInstructionHandler(TemporaryInstructionService s)
    : IRequestHandler<GetTemporaryInstructionQuery, Response<TemporaryInstructionModel>>
{
    public Task<Response<TemporaryInstructionModel>> Handle(GetTemporaryInstructionQuery r, CancellationToken ct) =>
        s.GetAsync(r.RegisterEntryId, r.CorrelationId, ct);
}
