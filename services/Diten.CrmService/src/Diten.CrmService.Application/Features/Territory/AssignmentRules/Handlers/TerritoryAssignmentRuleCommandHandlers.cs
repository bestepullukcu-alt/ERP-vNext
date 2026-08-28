using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules.Handlers;

/// <summary>Shared state/guards for the FU03 rule write handlers. Rules are model-scoped and, like nodes, may only be
/// mutated while the model is DRAFT (pack §20 immutability) — an active/inactive/archived model returns 409.</summary>
public abstract class TerritoryAssignmentRuleWriteHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly ITerritoryModelRepository Models;
    protected readonly ITerritoryNodeRepository Nodes;
    protected readonly ITerritoryAssignmentRuleRepository Rules;
    protected readonly ITerritoryReferenceValidator References;

    protected TerritoryAssignmentRuleWriteHandlerBase(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules,
        ITerritoryReferenceValidator references)
    {
        Tenant = tenant;
        Models = models;
        Nodes = nodes;
        Rules = rules;
        References = references;
    }

    protected async Task<(TerritoryModel? Model, string? Error, int Status)> LoadDraftModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken)
    {
        var model = await Models.GetByIdAsync(tenantId, modelId, cancellationToken);
        if (model is null)
        {
            return (null, "Territory model not found.", 404);
        }

        if (!string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Assignment rules can only be changed on a draft territory model.", 409);
        }

        return (model, null, 0);
    }
}

public sealed class CreateTerritoryAssignmentRuleHandler
    : TerritoryAssignmentRuleWriteHandlerBase, IRequestHandler<CreateTerritoryAssignmentRuleCommand, Response<Guid>>
{
    public CreateTerritoryAssignmentRuleHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, rules, references) { }

    public async Task<Response<Guid>> Handle(CreateTerritoryAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadDraftModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<Guid>.Fail(error!, status);
        }

        var ruleCode = request.RuleCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            return Response<Guid>.Fail("RuleCode is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<Guid>.Fail("Name is required.", 400);
        }

        var node = await Nodes.GetByIdAsync(tenantId, request.ModelId, request.TerritoryId, cancellationToken);
        var criteria = TerritoryAssignmentRuleValidation.Normalize(request.Criteria);
        var validationError = await TerritoryAssignmentRuleValidation.ValidateAsync(
            References, model, node, request.RuleType?.Trim() ?? string.Empty, request.ConflictPolicy?.Trim() ?? string.Empty,
            criteria, request.EffectiveFrom, request.EffectiveTo, cancellationToken);
        if (validationError is not null)
        {
            return Response<Guid>.Fail(validationError.Message, validationError.StatusCode);
        }

        if (await Rules.ExistsByCodeAsync(tenantId, request.ModelId, ruleCode, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("RuleCode already exists in this model.", 409);
        }

        var rule = new TerritoryAssignmentRule
        {
            TenantId = tenantId,
            ModelId = request.ModelId,
            TerritoryId = request.TerritoryId,
            RuleCode = ruleCode,
            Name = request.Name.Trim(),
            RuleType = request.RuleType!.Trim(),
            ConflictPolicy = request.ConflictPolicy!.Trim(),
            Priority = request.Priority,
            IsEnabled = request.IsEnabled,
            Criteria = criteria,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CorrelationId = request.CorrelationId?.Trim()
        };

        await Rules.InsertAsync(rule, cancellationToken);
        return Response<Guid>.Success(rule.Id, 201);
    }
}

public sealed class UpdateTerritoryAssignmentRuleHandler
    : TerritoryAssignmentRuleWriteHandlerBase, IRequestHandler<UpdateTerritoryAssignmentRuleCommand, Response<bool>>
{
    public UpdateTerritoryAssignmentRuleHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, rules, references) { }

    public async Task<Response<bool>> Handle(UpdateTerritoryAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadDraftModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var rule = await Rules.GetByIdAsync(tenantId, request.ModelId, request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Response<bool>.Fail("Assignment rule not found.", 404);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<bool>.Fail("Name is required.", 400);
        }

        var node = await Nodes.GetByIdAsync(tenantId, request.ModelId, request.TerritoryId, cancellationToken);
        var criteria = TerritoryAssignmentRuleValidation.Normalize(request.Criteria);
        var validationError = await TerritoryAssignmentRuleValidation.ValidateAsync(
            References, model, node, request.RuleType?.Trim() ?? string.Empty, request.ConflictPolicy?.Trim() ?? string.Empty,
            criteria, request.EffectiveFrom, request.EffectiveTo, cancellationToken);
        if (validationError is not null)
        {
            return Response<bool>.Fail(validationError.Message, validationError.StatusCode);
        }

        rule.TerritoryId = request.TerritoryId;
        rule.Name = request.Name.Trim();
        rule.RuleType = request.RuleType!.Trim();
        rule.ConflictPolicy = request.ConflictPolicy!.Trim();
        rule.Priority = request.Priority;
        rule.IsEnabled = request.IsEnabled;
        rule.Criteria = criteria;
        rule.EffectiveFrom = request.EffectiveFrom;
        rule.EffectiveTo = request.EffectiveTo;
        rule.CorrelationId = request.CorrelationId?.Trim();
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        await Rules.UpdateAsync(rule, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class SoftDeleteTerritoryAssignmentRuleHandler
    : TerritoryAssignmentRuleWriteHandlerBase, IRequestHandler<SoftDeleteTerritoryAssignmentRuleCommand, Response<bool>>
{
    public SoftDeleteTerritoryAssignmentRuleHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, rules, references) { }

    public async Task<Response<bool>> Handle(SoftDeleteTerritoryAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadDraftModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var rule = await Rules.GetByIdAsync(tenantId, request.ModelId, request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Response<bool>.Fail("Assignment rule not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        rule.IsDeleted = true;
        rule.DeletedAt = now;
        rule.UpdatedAt = now;
        rule.CorrelationId = request.CorrelationId?.Trim();
        await Rules.UpdateAsync(rule, cancellationToken);

        return Response<bool>.Success(true);
    }
}
