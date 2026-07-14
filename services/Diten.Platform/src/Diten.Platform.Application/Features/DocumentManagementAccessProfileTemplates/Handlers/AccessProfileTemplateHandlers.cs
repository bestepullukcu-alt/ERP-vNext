using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.Commands;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.Handlers;

public sealed class DryRunAccessProfileTemplatesHandler(AccessProfilePolicyPlanner planner)
    : IRequestHandler<DryRunAccessProfileTemplatesCommand, Response<AccessProfileTemplateSummary>>
{
    public Task<Response<AccessProfileTemplateSummary>> Handle(DryRunAccessProfileTemplatesCommand request, CancellationToken ct) =>
        planner.RunAsync(request.Request with { DryRun = true }, apply: false, request.CorrelationId, ct);
}

public sealed class ApplyAccessProfileTemplatesHandler(AccessProfilePolicyPlanner planner)
    : IRequestHandler<ApplyAccessProfileTemplatesCommand, Response<AccessProfileTemplateSummary>>
{
    public Task<Response<AccessProfileTemplateSummary>> Handle(ApplyAccessProfileTemplatesCommand request, CancellationToken ct) =>
        planner.RunAsync(request.Request with { DryRun = false }, apply: true, request.CorrelationId, ct);
}
