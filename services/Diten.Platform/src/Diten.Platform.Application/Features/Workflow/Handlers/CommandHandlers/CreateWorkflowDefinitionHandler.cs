using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class CreateWorkflowDefinitionHandler
    : IRequestHandler<CreateWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailDto>>
{
    private readonly IWorkflowTemplateRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateWorkflowDefinitionHandler(IWorkflowTemplateRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkflowDefinitionDetailDto>> Handle(
        CreateWorkflowDefinitionCommand request,
        CancellationToken ct)
    {
        var templateCode = request.Request.TemplateCode.Trim();
        var name = request.Request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Request.Description)
            ? null
            : request.Request.Description.Trim();

        // Tenant-scoped duplicate guard: the same TemplateCode is unique within a tenant, but allowed in
        // a different tenant (the repository read is filtered by the server-resolved tenant context).
        var existing = await _repository.GetByTemplateCodeAsync(templateCode, ct);
        if (existing is not null)
        {
            return Response<WorkflowDefinitionDetailDto>.Fail(
                $"A workflow definition with code '{templateCode}' already exists.",
                409,
                WorkflowReasonCodes.DuplicateTemplateCode,
                request.CorrelationId);
        }

        // TenantId is set from the server-side tenant context (never from the client payload). The
        // repository re-asserts it from context on insert.
        var template = new WorkflowTemplate
        {
            TenantId = _tenantContext.TenantId,
            TemplateCode = templateCode,
            Name = name,
            Description = description,
            Status = WorkflowTemplateStatus.Draft
        };

        var created = await _repository.CreateAsync(template, ct);

        return Response<WorkflowDefinitionDetailDto>.Success(
            WorkflowDefinitionMapper.ToDetail(created),
            201,
            request.CorrelationId);
    }
}
