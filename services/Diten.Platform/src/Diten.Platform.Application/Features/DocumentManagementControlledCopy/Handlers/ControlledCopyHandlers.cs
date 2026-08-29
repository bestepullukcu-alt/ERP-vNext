using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Handlers;

// MOD-0029-FU17 — thin MediatR handlers delegating to DocumentControlledCopyService.

public sealed class RegisterControlledCopyHandler(DocumentControlledCopyService s)
    : IRequestHandler<RegisterControlledCopyCommand, Response<ControlledCopyModel>>
{
    public Task<Response<ControlledCopyModel>> Handle(RegisterControlledCopyCommand r, CancellationToken ct) =>
        s.RegisterCopyAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class WithdrawControlledCopyHandler(DocumentControlledCopyService s)
    : IRequestHandler<WithdrawControlledCopyCommand, Response<ControlledCopyModel>>
{
    public Task<Response<ControlledCopyModel>> Handle(WithdrawControlledCopyCommand r, CancellationToken ct) =>
        s.WithdrawAsync(r.RegisterEntryId, r.CopyId, r.Input, r.CorrelationId, ct);
}

public sealed class ReconcileControlledCopyHandler(DocumentControlledCopyService s)
    : IRequestHandler<ReconcileControlledCopyCommand, Response<ControlledCopyModel>>
{
    public Task<Response<ControlledCopyModel>> Handle(ReconcileControlledCopyCommand r, CancellationToken ct) =>
        s.ReconcileAsync(r.RegisterEntryId, r.CopyId, r.Input, r.CorrelationId, ct);
}

public sealed class MarkControlledCopyMissingHandler(DocumentControlledCopyService s)
    : IRequestHandler<MarkControlledCopyMissingCommand, Response<ControlledCopyModel>>
{
    public Task<Response<ControlledCopyModel>> Handle(MarkControlledCopyMissingCommand r, CancellationToken ct) =>
        s.MarkMissingAsync(r.RegisterEntryId, r.CopyId, r.Input, r.CorrelationId, ct);
}

public sealed class MarkControlledCopyObsoleteHandler(DocumentControlledCopyService s)
    : IRequestHandler<MarkControlledCopyObsoleteCommand, Response<ControlledCopyModel>>
{
    public Task<Response<ControlledCopyModel>> Handle(MarkControlledCopyObsoleteCommand r, CancellationToken ct) =>
        s.MarkObsoleteAsync(r.RegisterEntryId, r.CopyId, r.Input, r.CorrelationId, ct);
}

public sealed class GenerateWithdrawalPlanHandler(DocumentControlledCopyService s)
    : IRequestHandler<GenerateWithdrawalPlanCommand, Response<WithdrawalPlanModel>>
{
    public Task<Response<WithdrawalPlanModel>> Handle(GenerateWithdrawalPlanCommand r, CancellationToken ct) =>
        s.GeneratePlanAsync(r.RegisterEntryId, r.Input, r.CorrelationId, ct);
}

public sealed class CompleteWithdrawalPlanHandler(DocumentControlledCopyService s)
    : IRequestHandler<CompleteWithdrawalPlanCommand, Response<WithdrawalPlanModel>>
{
    public Task<Response<WithdrawalPlanModel>> Handle(CompleteWithdrawalPlanCommand r, CancellationToken ct) =>
        s.CompletePlanAsync(r.RegisterEntryId, r.PlanId, r.Input, r.CorrelationId, ct);
}

public sealed class EvaluateObsoleteCopyReconciliationHandler(DocumentControlledCopyService s)
    : IRequestHandler<EvaluateObsoleteCopyReconciliationCommand, Response<IReadOnlyList<ObsoleteCopyFindingModel>>>
{
    public Task<Response<IReadOnlyList<ObsoleteCopyFindingModel>>> Handle(EvaluateObsoleteCopyReconciliationCommand r, CancellationToken ct) =>
        s.EvaluateReconciliationAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class ResolveObsoleteCopyFindingHandler(DocumentControlledCopyService s)
    : IRequestHandler<ResolveObsoleteCopyFindingCommand, Response<ObsoleteCopyFindingModel>>
{
    public Task<Response<ObsoleteCopyFindingModel>> Handle(ResolveObsoleteCopyFindingCommand r, CancellationToken ct) =>
        s.ResolveFindingAsync(r.RegisterEntryId, r.FindingId, r.Input, r.CorrelationId, ct);
}

public sealed class GetControlledCopiesHandler(DocumentControlledCopyService s)
    : IRequestHandler<GetControlledCopiesQuery, Response<IReadOnlyList<ControlledCopyModel>>>
{
    public Task<Response<IReadOnlyList<ControlledCopyModel>>> Handle(GetControlledCopiesQuery r, CancellationToken ct) =>
        s.ListCopiesAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class GetWithdrawalPlansHandler(DocumentControlledCopyService s)
    : IRequestHandler<GetWithdrawalPlansQuery, Response<IReadOnlyList<WithdrawalPlanModel>>>
{
    public Task<Response<IReadOnlyList<WithdrawalPlanModel>>> Handle(GetWithdrawalPlansQuery r, CancellationToken ct) =>
        s.ListPlansAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class GetCopyWithdrawalReadinessHandler(DocumentControlledCopyService s)
    : IRequestHandler<GetCopyWithdrawalReadinessQuery, Response<CopyWithdrawalReadinessModel>>
{
    public Task<Response<CopyWithdrawalReadinessModel>> Handle(GetCopyWithdrawalReadinessQuery r, CancellationToken ct) =>
        s.GetReadinessAsync(r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class GetObsoleteCopyFindingsHandler(DocumentControlledCopyService s)
    : IRequestHandler<GetObsoleteCopyFindingsQuery, Response<IReadOnlyList<ObsoleteCopyFindingModel>>>
{
    public Task<Response<IReadOnlyList<ObsoleteCopyFindingModel>>> Handle(GetObsoleteCopyFindingsQuery r, CancellationToken ct) =>
        s.ListFindingsAsync(r.RegisterEntryId, r.CorrelationId, ct);
}
