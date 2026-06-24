using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetSlaEscalationRulesHandler
    : IRequestHandler<GetSlaEscalationRulesQuery, Response<IReadOnlyList<SlaEscalationRuleDto>>>
{
    private readonly ISlaEscalationRuleRepository _rules;

    public GetSlaEscalationRulesHandler(ISlaEscalationRuleRepository rules) => _rules = rules;

    public async Task<Response<IReadOnlyList<SlaEscalationRuleDto>>> Handle(
        GetSlaEscalationRulesQuery request,
        CancellationToken ct)
    {
        var rules = request.TemplateId.HasValue && request.TemplateId.Value != Guid.Empty
            ? await _rules.ListActiveByTemplateIdAsync(request.TemplateId.Value, ct)
            : await _rules.ListActiveAsync(ct);
        return Response<IReadOnlyList<SlaEscalationRuleDto>>.Success(
            rules.Select(WorkflowDefinitionMapper.ToSlaRule).ToList(),
            correlationId: request.CorrelationId);
    }
}
