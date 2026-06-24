using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class PublishWorkflowDefinitionHandler
    : IRequestHandler<PublishWorkflowDefinitionCommand, Response<PublishWorkflowDefinitionResponse>>
{
    private readonly IWorkflowTemplateRepository _templateRepository;
    private readonly IWorkflowTemplateVersionRepository _versionRepository;
    private readonly ISlaEscalationRuleRepository? _slaRules;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public PublishWorkflowDefinitionHandler(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowTemplateVersionRepository versionRepository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext,
        ISlaEscalationRuleRepository? slaRules = null)
    {
        _templateRepository = templateRepository;
        _versionRepository = versionRepository;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
        _slaRules = slaRules;
    }

    public async Task<Response<PublishWorkflowDefinitionResponse>> Handle(
        PublishWorkflowDefinitionCommand request,
        CancellationToken ct)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(request.Request.DefinitionJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Invalid Definition JSON format.",
                400,
                WorkflowReasonCodes.ValidationFailed,
                request.CorrelationId);
        }

        var template = await _templateRepository.GetByIdAsync(request.TemplateId, ct);
        if (template is null)
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        if (template.Status is not (WorkflowTemplateStatus.Draft or WorkflowTemplateStatus.Reviewed or WorkflowTemplateStatus.Published))
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Workflow definition cannot be published from its current status.",
                409,
                WorkflowReasonCodes.WorkflowTemplatePublishConflict,
                request.CorrelationId);
        }

        if (request.Request.ExpectedTemplateVersion.HasValue &&
            template.Version != request.Request.ExpectedTemplateVersion.Value)
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Stale workflow definition version.",
                409,
                WorkflowReasonCodes.WorkflowTemplateConcurrencyConflict,
                request.CorrelationId);
        }

        var nextVersionNumber = await _versionRepository.GetLatestVersionNumberAsync(template.Id, ct) + 1;
        if (await _versionRepository.ExistsVersionNumberAsync(template.Id, nextVersionNumber, ct))
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Workflow definition version number already exists.",
                409,
                WorkflowReasonCodes.WorkflowTemplatePublishConflict,
                request.CorrelationId);
        }

        var now = DateTime.UtcNow;
        var publishedBy = _currentUserContext.ActorName;
        var version = new WorkflowTemplateVersion
        {
            TenantId = _tenantContext.TenantId,
            TemplateId = template.Id,
            VersionNumber = nextVersionNumber,
            DefinitionJson = request.Request.DefinitionJson.Trim(),
            SchemaVersion = request.Request.SchemaVersion.Trim(),
            ExpressionVersion = request.Request.ExpressionVersion.Trim(),
            Status = WorkflowTemplateVersionStatus.Published,
            IsImmutable = true,
            PublishedAt = now,
            PublishedBy = publishedBy,
            PublishReason = string.IsNullOrWhiteSpace(request.Request.PublishReason)
                ? null
                : request.Request.PublishReason.Trim()
        };

        var created = await _versionRepository.CreateAsync(version, ct);
        var expectedTemplateVersion = template.Version;
        template.ActivePublishedVersionId = created.Id;
        template.CurrentVersionId = created.Id;
        template.Status = WorkflowTemplateStatus.Published;

        var updated = await _templateRepository.UpdateAsync(template, expectedTemplateVersion, ct);
        if (!updated)
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "Stale workflow definition version.",
                409,
                WorkflowReasonCodes.WorkflowTemplateConcurrencyConflict,
                request.CorrelationId);
        }

        if (_slaRules is not null)
        {
            var steps = WorkflowDefinitionRuntimePlan.FromVersion(created);
            await _slaRules.DeactivateRulesForTemplateAsync(template.Id, ct);
            foreach (var step in steps)
            {
                if (step.DueInMinutes.HasValue && step.DueInMinutes.Value > 0)
                {
                    var rule = new SlaEscalationRule
                    {
                        TenantId = _tenantContext.TenantId,
                        TemplateId = template.Id,
                        StageCode = step.StageCode,
                        StepCode = step.StepCode,
                        DueInMinutes = step.DueInMinutes.Value,
                        EscalateAfterMinutes = step.EscalateAfterMinutes ?? step.DueInMinutes.Value,
                        TimeoutAfterMinutes = step.TimeoutAfterMinutes,
                        EscalationPrincipalIds = (step.EscalationPrincipalIds ?? Array.Empty<string>()).ToList(),
                        IsActive = true
                    };
                    await _slaRules.CreateAsync(rule, ct);
                }
            }
        }

        var response = new PublishWorkflowDefinitionResponse(
            template.Id,
            created.Id,
            created.VersionNumber,
            created.IsImmutable,
            created.Status.ToString(),
            created.PublishedAt,
            created.PublishedBy,
            request.CorrelationId);

        return Response<PublishWorkflowDefinitionResponse>.Success(response, correlationId: request.CorrelationId);
    }
}
