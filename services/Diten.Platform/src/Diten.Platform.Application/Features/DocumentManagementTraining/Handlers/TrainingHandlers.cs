using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTraining.Commands;
using Diten.Platform.Application.Features.DocumentManagementTraining.Queries;
using Diten.Platform.Application.Features.DocumentManagementTraining.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Handlers;

// MOD-0029-FU11 — thin MediatR handlers delegating to DocumentTrainingService.

public sealed class ResolveTrainingMatrixHandler(DocumentTrainingService service)
    : IRequestHandler<ResolveTrainingMatrixCommand, Response<IReadOnlyList<TrainingRequirementModel>>>
{
    public Task<Response<IReadOnlyList<TrainingRequirementModel>>> Handle(ResolveTrainingMatrixCommand request, CancellationToken ct) =>
        service.ResolveMatrixAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class AddManualTrainingRequirementHandler(DocumentTrainingService service)
    : IRequestHandler<AddManualTrainingRequirementCommand, Response<TrainingRequirementModel>>
{
    public Task<Response<TrainingRequirementModel>> Handle(AddManualTrainingRequirementCommand request, CancellationToken ct) =>
        service.AddManualRequirementAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class AssignTrainingHandler(DocumentTrainingService service)
    : IRequestHandler<AssignTrainingCommand, Response<TrainingAssignmentModel>>
{
    public Task<Response<TrainingAssignmentModel>> Handle(AssignTrainingCommand request, CancellationToken ct) =>
        service.AssignAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class CompleteTrainingHandler(DocumentTrainingService service)
    : IRequestHandler<CompleteTrainingCommand, Response<TrainingAssignmentModel>>
{
    public Task<Response<TrainingAssignmentModel>> Handle(CompleteTrainingCommand request, CancellationToken ct) =>
        service.CompleteAsync(request.RegisterEntryId, request.AssignmentId, request.Input, request.CorrelationId, ct);
}

public sealed class RecordTrainingEffectivenessHandler(DocumentTrainingService service)
    : IRequestHandler<RecordTrainingEffectivenessCommand, Response<TrainingAssignmentModel>>
{
    public Task<Response<TrainingAssignmentModel>> Handle(RecordTrainingEffectivenessCommand request, CancellationToken ct) =>
        service.RecordEffectivenessAsync(request.RegisterEntryId, request.AssignmentId, request.Input, request.CorrelationId, ct);
}

public sealed class RestrictTrainingHandler(DocumentTrainingService service)
    : IRequestHandler<RestrictTrainingCommand, Response<TrainingAssignmentModel>>
{
    public Task<Response<TrainingAssignmentModel>> Handle(RestrictTrainingCommand request, CancellationToken ct) =>
        service.RestrictAsync(request.RegisterEntryId, request.AssignmentId, request.Input, request.CorrelationId, ct);
}

public sealed class GetTrainingRequirementsHandler(DocumentTrainingService service)
    : IRequestHandler<GetTrainingRequirementsQuery, Response<IReadOnlyList<TrainingRequirementModel>>>
{
    public Task<Response<IReadOnlyList<TrainingRequirementModel>>> Handle(GetTrainingRequirementsQuery request, CancellationToken ct) =>
        service.GetRequirementsAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetTrainingReadinessHandler(DocumentTrainingService service)
    : IRequestHandler<GetTrainingReadinessQuery, Response<TrainingReadinessModel>>
{
    public Task<Response<TrainingReadinessModel>> Handle(GetTrainingReadinessQuery request, CancellationToken ct) =>
        service.GetReadinessAsync(request.RegisterEntryId, request.CorrelationId, ct);
}
