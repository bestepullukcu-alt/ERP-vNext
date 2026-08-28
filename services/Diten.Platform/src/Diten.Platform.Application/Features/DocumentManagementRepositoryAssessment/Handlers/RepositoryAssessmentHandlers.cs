using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Commands;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Queries;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Handlers;

// MOD-0029-FU16 — thin MediatR handlers delegating to DocumentRepositoryAssessmentService.

public sealed class CreateRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<CreateRepositoryAssessmentCommand, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(CreateRepositoryAssessmentCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class UpdateRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<UpdateRepositoryAssessmentCommand, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(UpdateRepositoryAssessmentCommand r, CancellationToken ct) =>
        s.UpdateAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class EvaluateRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<EvaluateRepositoryAssessmentCommand, Response<RepositoryAssessmentReadinessModel>>
{
    public Task<Response<RepositoryAssessmentReadinessModel>> Handle(EvaluateRepositoryAssessmentCommand r, CancellationToken ct) =>
        s.EvaluateAsync(r.Id, r.CorrelationId, ct);
}

public sealed class ApproveRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<ApproveRepositoryAssessmentCommand, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(ApproveRepositoryAssessmentCommand r, CancellationToken ct) =>
        s.ApproveAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RejectRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<RejectRepositoryAssessmentCommand, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(RejectRepositoryAssessmentCommand r, CancellationToken ct) =>
        s.RejectAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class LinkRepositoryAssessmentToRegisterEntryHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<LinkRepositoryAssessmentToRegisterEntryCommand, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(LinkRepositoryAssessmentToRegisterEntryCommand r, CancellationToken ct) =>
        s.LinkToRegisterAsync(r.RegisterEntryId, r.Input.RepositoryAssessmentId, r.CorrelationId, ct);
}

public sealed class GetRepositoryAssessmentsHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<GetRepositoryAssessmentsQuery, Response<IReadOnlyList<RepositoryAssessmentModel>>>
{
    public Task<Response<IReadOnlyList<RepositoryAssessmentModel>>> Handle(GetRepositoryAssessmentsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetRepositoryAssessmentByIdHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<GetRepositoryAssessmentByIdQuery, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(GetRepositoryAssessmentByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetRepositoryAssessmentFindingsHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<GetRepositoryAssessmentFindingsQuery, Response<IReadOnlyList<RepositoryAssessmentFindingModel>>>
{
    public Task<Response<IReadOnlyList<RepositoryAssessmentFindingModel>>> Handle(GetRepositoryAssessmentFindingsQuery r, CancellationToken ct) =>
        s.GetFindingsAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetLinkedRepositoryAssessmentHandler(DocumentRepositoryAssessmentService s)
    : IRequestHandler<GetLinkedRepositoryAssessmentQuery, Response<RepositoryAssessmentModel>>
{
    public Task<Response<RepositoryAssessmentModel>> Handle(GetLinkedRepositoryAssessmentQuery r, CancellationToken ct) =>
        s.GetLinkedAsync(r.RegisterEntryId, r.CorrelationId, ct);
}
