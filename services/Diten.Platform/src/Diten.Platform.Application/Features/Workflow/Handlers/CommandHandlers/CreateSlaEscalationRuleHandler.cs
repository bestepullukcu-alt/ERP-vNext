using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class CreateSlaEscalationRuleHandler
    : IRequestHandler<CreateSlaEscalationRuleCommand, Response<SlaEscalationRuleDto>>
{
    private readonly IWorkflowTemplateRepository _templates;
    private readonly ISlaEscalationRuleRepository _rules;
    private readonly ITenantContext _tenantContext;

    public CreateSlaEscalationRuleHandler(
        IWorkflowTemplateRepository templates,
        ISlaEscalationRuleRepository rules,
        ITenantContext tenantContext)
    {
        _templates = templates;
        _rules = rules;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SlaEscalationRuleDto>> Handle(
        CreateSlaEscalationRuleCommand request,
        CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(request.Request.TemplateId, ct);
        if (template is null)
        {
            return Response<SlaEscalationRuleDto>.Fail(
                "Workflow template not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var stageCode = request.Request.StageCode.Trim();
        var stepCode = request.Request.StepCode.Trim();
        var existing = await _rules.FindForStepAsync(template.Id, stageCode, stepCode, ct);
        if (existing is not null)
        {
            return Response<SlaEscalationRuleDto>.Fail(
                "SLA rule already exists for this workflow step.",
                409,
                WorkflowReasonCodes.WorkflowSlaRuleConflict,
                request.CorrelationId);
        }

        var rule = new SlaEscalationRule
        {
            TenantId = _tenantContext.TenantId,
            TemplateId = template.Id,
            StageCode = stageCode,
            StepCode = stepCode,
            DueInMinutes = request.Request.DueInMinutes,
            EscalateAfterMinutes = request.Request.EscalateAfterMinutes,
            TimeoutAfterMinutes = request.Request.TimeoutAfterMinutes,
            EscalationPrincipalIds = NormalizePrincipals(request.Request.EscalationPrincipalIds),
            IsActive = true
        };

        var created = await _rules.CreateAsync(rule, ct);
        return Response<SlaEscalationRuleDto>.Success(
            WorkflowDefinitionMapper.ToSlaRule(created),
            201,
            request.CorrelationId);
    }

    private static List<string> NormalizePrincipals(IReadOnlyList<string> principals) =>
        principals
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
}
