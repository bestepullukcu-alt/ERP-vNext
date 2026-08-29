using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models.Handlers;

public sealed class CreateTerritoryModelHandler : IRequestHandler<CreateTerritoryModelCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryReferenceValidator _references;
    private readonly ITerritoryNodeRepository? _nodes;
    private readonly ITerritoryAssignmentRuleRepository? _rules;
    private readonly ITerritoryDraftCloneUnitOfWork? _cloneUnitOfWork;

    public CreateTerritoryModelHandler(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryReferenceValidator references,
        ITerritoryNodeRepository? nodes = null,
        ITerritoryAssignmentRuleRepository? rules = null,
        ITerritoryDraftCloneUnitOfWork? cloneUnitOfWork = null)
    {
        _tenant = tenant;
        _models = models;
        _references = references;
        _nodes = nodes;
        _rules = rules;
        _cloneUnitOfWork = cloneUnitOfWork;
    }

    public async Task<Response<Guid>> Handle(CreateTerritoryModelCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        // FU01 always creates a draft — but the status set MUST be published (fail-closed, no default lifecycle list).
        var statusCheck = await _references.ValidateValueAsync(
            TerritoryReferenceSets.TerritoryModelStatus, TerritoryReferenceSets.DraftStatus, cancellationToken);
        if (statusCheck != ReferenceValidationStatus.Valid)
        {
            return Response<Guid>.Fail(ReferenceError(TerritoryReferenceSets.TerritoryModelStatus, statusCheck), 400);
        }

        var modelCode = request.ModelCode.Trim();
        if (await _models.ExistsByCodeAsync(tenantId, modelCode, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("ModelCode already exists for this tenant.", 409);
        }

        TerritoryModel? source = null;
        if (request.BasedOnModelId is { } basedOn)
        {
            source = await _models.GetByIdAsync(tenantId, basedOn, cancellationToken);
            if (source is null)
            {
                return Response<Guid>.Fail("BasedOnModelId does not resolve to a model in this tenant.", 404);
            }

            if (!string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(source.Status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                return Response<Guid>.Fail("Only an active or inactive model can be used to create a draft version.", 409);
            }

            if (_nodes is null || _rules is null || _cloneUnitOfWork is null)
            {
                return Response<Guid>.Fail("Territory model versioning services are not available.", 500);
            }
        }

        // FU02A: business-unit scopes are fail-closed validated against MOD-0048 published values (no fallback).
        var (businessScopes, scopeError) = await TerritoryBusinessScopeResolver.ResolveAsync(
            request.BusinessScopes, _references, cancellationToken);
        if (scopeError is not null)
        {
            return Response<Guid>.Fail(scopeError, 400);
        }

        var model = new TerritoryModel
        {
            TenantId = tenantId,
            ModelCode = modelCode,
            Name = request.Name.Trim(),
            CountryScope = request.CountryScope?.Trim(),
            DivisionScope = request.DivisionScope?.Trim(),
            BusinessScopes = businessScopes,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = TerritoryReferenceSets.DraftStatus,
            VersionNumber = source is null ? 1 : source.VersionNumber + 1,
            BasedOnModelId = request.BasedOnModelId,
            ChangeReason = request.ChangeReason?.Trim(),
            CorrelationId = request.CorrelationId?.Trim()
        };

        if (source is null)
        {
            await _models.InsertAsync(model, cancellationToken);
            return Response<Guid>.Success(model.Id, 201);
        }

        var sourceNodes = await _nodes!.ListByModelAsync(tenantId, source.Id, cancellationToken);
        var nodeIdMap = sourceNodes.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        var clonedNodes = sourceNodes.Select(node => new TerritoryNode
        {
            Id = nodeIdMap[node.Id],
            TenantId = tenantId,
            ModelId = model.Id,
            ParentTerritoryId = node.ParentTerritoryId is { } parentId ? nodeIdMap.GetValueOrDefault(parentId) : null,
            TerritoryCode = node.TerritoryCode,
            Name = node.Name,
            TerritoryLevel = node.TerritoryLevel,
            CountryCode = node.CountryCode,
            DivisionCode = node.DivisionCode,
            RegionCode = node.RegionCode,
            AreaCode = node.AreaCode,
            ZoneCode = node.ZoneCode,
            MicroZoneCode = node.MicroZoneCode,
            Status = TerritoryReferenceSets.DraftStatus,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            SortOrder = node.SortOrder,
            MicroZoneProfile = node.MicroZoneProfile is null ? null : new MicroZoneProfile
            {
                AnchorAccountId = node.MicroZoneProfile.AnchorAccountId,
                ClusterNotes = node.MicroZoneProfile.ClusterNotes,
                PlanningCenterType = node.MicroZoneProfile.PlanningCenterType
            },
            CorrelationId = request.CorrelationId?.Trim()
        }).ToList();

        var sourceRules = await _rules!.ListByModelAsync(tenantId, source.Id, cancellationToken);
        if (sourceRules.Any(rule => !nodeIdMap.ContainsKey(rule.TerritoryId)))
        {
            return Response<Guid>.Fail("A source assignment rule targets a territory node that cannot be mapped.", 409);
        }

        var clonedRules = sourceRules.Select(rule => new TerritoryAssignmentRule
        {
            TenantId = tenantId,
            ModelId = model.Id,
            TerritoryId = nodeIdMap[rule.TerritoryId],
            RuleCode = rule.RuleCode,
            Name = rule.Name,
            RuleType = rule.RuleType,
            ConflictPolicy = rule.ConflictPolicy,
            Priority = rule.Priority,
            IsEnabled = rule.IsEnabled,
            Criteria = CloneCriteria(rule.Criteria),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CorrelationId = request.CorrelationId?.Trim()
        }).ToList();

        await _cloneUnitOfWork!.CommitAsync(model, clonedNodes, clonedRules, cancellationToken);
        return Response<Guid>.Success(model.Id, 201);
    }

    private static TerritoryRuleCriteria CloneCriteria(TerritoryRuleCriteria source) => new()
    {
        CountryRefs = [.. source.CountryRefs],
        CityRefs = [.. source.CityRefs],
        DistrictRefs = [.. source.DistrictRefs],
        AccountTypes = [.. source.AccountTypes],
        AccountCategories = [.. source.AccountCategories],
        AccountStatuses = [.. source.AccountStatuses],
        IncludeAccountIds = [.. source.IncludeAccountIds],
        ExcludeAccountIds = [.. source.ExcludeAccountIds]
    };

    internal static string ReferenceError(string setCode, ReferenceValidationStatus status) => status switch
    {
        ReferenceValidationStatus.SetMissing => $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
        _ => $"'{setCode}' does not contain the required published value."
    };
}
