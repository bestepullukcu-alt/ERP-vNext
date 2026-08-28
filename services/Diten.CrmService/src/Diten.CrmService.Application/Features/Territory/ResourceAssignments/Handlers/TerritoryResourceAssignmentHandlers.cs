using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments.Handlers;

/// <summary>Shared plumbing for the FU04 handlers. Like nodes and rules, resource assignments may only be mutated
/// while the model is DRAFT (pack §20 immutability of an active model).</summary>
public abstract class TerritoryResourceAssignmentHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly ITerritoryModelRepository Models;
    protected readonly ITerritoryNodeRepository Nodes;
    protected readonly ITerritoryResourceAssignmentRepository Assignments;
    protected readonly ITerritoryReferenceValidator References;

    protected TerritoryResourceAssignmentHandlerBase(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments,
        ITerritoryReferenceValidator references)
    {
        Tenant = tenant;
        Models = models;
        Nodes = nodes;
        Assignments = assignments;
        References = references;
    }

    protected async Task<(TerritoryModel? Model, string? Error, int Status)> LoadMutableModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken, bool draftOnly = false)
    {
        var model = await Models.GetByIdAsync(tenantId, modelId, cancellationToken);
        if (model is null)
        {
            return (null, "Territory model not found.", 404);
        }

        if (model.IsDeleted)
        {
            return (null, "A soft-deleted territory model cannot be changed.", 409);
        }

        var draft = string.Equals(model.Status, TerritoryReferenceSets.DraftStatus, StringComparison.OrdinalIgnoreCase);
        var active = string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase);
        if ((draftOnly && !draft) || (!draftOnly && !draft && !active))
        {
            return (null, draftOnly
                ? "This operation is only allowed on a draft territory model."
                : "Resource assignments can only be changed on a draft or active territory model.", 409);
        }

        return (model, null, 0);
    }

    protected static bool IsOverride(string source)
        => string.Equals(source, "override", StringComparison.OrdinalIgnoreCase);

    protected static TerritoryResourceRef ToRef(TerritoryResourceRefInput input)
        => new()
        {
            ResourceId = input.ResourceId.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(input.ResourceType) ? "person" : input.ResourceType.Trim(),
            DisplayName = input.DisplayName.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim()
        };

    protected static TerritoryResourceValidationError? ValidateResource(TerritoryResourceRefInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.ResourceId))
        {
            return new TerritoryResourceValidationError("ResourceId is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            return new TerritoryResourceValidationError("Resource display name is required.", 400);
        }

        return null;
    }

    protected static TerritoryResourceAssignment Copy(TerritoryResourceAssignment source)
        => new()
        {
            Id = source.Id,
            TenantId = source.TenantId,
            ModelId = source.ModelId,
            TerritoryId = source.TerritoryId,
            Resource = new TerritoryResourceRef
            {
                ResourceId = source.Resource.ResourceId,
                ResourceType = source.Resource.ResourceType,
                DisplayName = source.Resource.DisplayName,
                Email = source.Resource.Email
            },
            Position = new TerritoryPositionRef
            {
                PositionId = source.Position.PositionId ?? source.PositionId,
                PositionCode = source.EffectivePositionCode,
                PositionTitle = source.EffectivePositionTitle,
                PositionType = source.Position.PositionType,
                SourceSystem = source.Position.SourceSystem,
                ValidationMode = source.Position.ValidationMode,
                PolicySource = source.Position.PolicySource
            },
            CoverageScope = source.CoverageScope,
            BusinessScopes = source.BusinessScopes.Select(s => new TerritoryBusinessScope
            {
                ScopeType = s.ScopeType,
                ScopeCode = s.ScopeCode
            }).ToList(),
            Status = source.Status,
            AssignmentSource = source.AssignmentSource,
            IsPrimary = source.IsPrimary,
            ValidFrom = source.ValidFrom,
            ValidTo = source.ValidTo,
            ChangeReason = source.ChangeReason,
            CorrelationId = source.CorrelationId,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Version = source.Version
        };
}

public sealed class CreateTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<CreateTerritoryResourceAssignmentCommand, Response<Guid>>
{
    public CreateTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<Guid>> Handle(CreateTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<Guid>.Fail(error!, status);
        }

        if (ValidateResource(request.Resource) is { } resourceError)
        {
            return Response<Guid>.Fail(resourceError.Message, resourceError.StatusCode);
        }

        TerritoryNode? node = null;
        if (request.TerritoryId is { } nodeId)
        {
            node = await Nodes.GetByIdAsync(tenantId, request.ModelId, nodeId, cancellationToken);
        }

        var operational = string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase);
        if (operational && string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            return Response<Guid>.Fail("Reason is required for an active-model assignment.", 400);
        }

        var (resolution, validationError) = await TerritoryResourceAssignmentValidation.ResolveAsync(
            References, model, node, request.TerritoryId, request.PositionId, request.PositionCode, request.PositionName,
            request.PositionType, request.PositionSourceSystem,
            request.CoverageScope, request.BusinessUnitScopeCodes, request.IsPrimary, request.AssignmentSource, request.ChangeReason,
            request.ValidFrom, request.ValidTo, cancellationToken, operational);

        if (resolution is null)
        {
            return Response<Guid>.Fail(validationError!.Message, validationError.StatusCode);
        }

        var assignment = new TerritoryResourceAssignment
        {
            TenantId = tenantId,
            ModelId = request.ModelId,
            TerritoryId = request.TerritoryId,
            Resource = ToRef(request.Resource!),
            Position = resolution.Position,
            PositionId = resolution.Position.PositionId,
            PositionCode = resolution.Position.PositionCode,
            PositionName = resolution.Position.PositionTitle,
            CoverageScope = resolution.CoverageScope,
            BusinessScopes = resolution.BusinessScopes,
            Status = resolution.Status,
            AssignmentSource = resolution.AssignmentSource,
            IsPrimary = resolution.IsPrimary,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            ChangeReason = request.ChangeReason?.Trim(),
            CorrelationId = request.CorrelationId?.Trim()
        };

        var existing = await Assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await Nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var conflict = TerritoryResourceConflictEngine.FindBlockingConflict(
            assignment, existing, nodes, allowOverride: IsOverride(resolution.AssignmentSource));

        if (conflict is not null)
        {
            return Response<Guid>.Fail(conflict.Message, 409);
        }

        await Assignments.InsertAsync(assignment, cancellationToken);
        return Response<Guid>.Success(assignment.Id, 201);
    }
}

public sealed class UpdateTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<UpdateTerritoryResourceAssignmentCommand, Response<bool>>
{
    public UpdateTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<bool>> Handle(UpdateTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken, draftOnly: true);
        if (model is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var assignment = await Assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Response<bool>.Fail("Resource assignment not found.", 404);
        }

        // Only a status whose metadata allows mutation may be edited; an ended/rejected row is history.
        var statusMeta = await References.GetValueMetadataAsync(
            TerritoryReferenceSets.TerritoryAssignmentStatus, assignment.Status, cancellationToken);
        if (!Common.ReferenceValidation.ReferenceMetadata.TryGetBool(
                statusMeta, TerritoryAssignmentMetadataKeys.AllowsMutation, out var mutable) || !mutable)
        {
            return Response<bool>.Fail(
                $"A '{assignment.Status}' resource assignment cannot be edited; end it and create a new one instead.", 409);
        }

        if (ValidateResource(request.Resource) is { } resourceError)
        {
            return Response<bool>.Fail(resourceError.Message, resourceError.StatusCode);
        }

        TerritoryNode? node = null;
        if (request.TerritoryId is { } nodeId)
        {
            node = await Nodes.GetByIdAsync(tenantId, request.ModelId, nodeId, cancellationToken);
        }

        var (resolution, validationError) = await TerritoryResourceAssignmentValidation.ResolveAsync(
            References, model, node, request.TerritoryId, request.PositionId, request.PositionCode, request.PositionName,
            request.PositionType, request.PositionSourceSystem,
            request.CoverageScope, request.BusinessUnitScopeCodes, request.IsPrimary, request.AssignmentSource, request.ChangeReason,
            request.ValidFrom, request.ValidTo, cancellationToken);

        if (resolution is null)
        {
            return Response<bool>.Fail(validationError!.Message, validationError.StatusCode);
        }

        var candidate = new TerritoryResourceAssignment
        {
            Id = assignment.Id,
            TenantId = tenantId,
            ModelId = request.ModelId,
            TerritoryId = request.TerritoryId,
            Resource = ToRef(request.Resource!),
            Position = resolution.Position,
            PositionId = resolution.Position.PositionId,
            PositionCode = resolution.Position.PositionCode,
            PositionName = resolution.Position.PositionTitle,
            CoverageScope = resolution.CoverageScope,
            BusinessScopes = resolution.BusinessScopes,
            Status = assignment.Status,
            AssignmentSource = resolution.AssignmentSource,
            IsPrimary = resolution.IsPrimary,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        var existing = await Assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodes = (await Nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var conflict = TerritoryResourceConflictEngine.FindBlockingConflict(
            candidate, existing, nodes, allowOverride: IsOverride(resolution.AssignmentSource));

        if (conflict is not null)
        {
            return Response<bool>.Fail(conflict.Message, 409);
        }

        assignment.TerritoryId = request.TerritoryId;
        assignment.Resource = candidate.Resource;
        assignment.Position = resolution.Position;
        assignment.PositionId = resolution.Position.PositionId;
        assignment.PositionCode = resolution.Position.PositionCode;
        assignment.PositionName = resolution.Position.PositionTitle;
        assignment.CoverageScope = resolution.CoverageScope;
        assignment.BusinessScopes = resolution.BusinessScopes;
        assignment.AssignmentSource = resolution.AssignmentSource;
        assignment.IsPrimary = resolution.IsPrimary;
        assignment.ValidFrom = request.ValidFrom;
        assignment.ValidTo = request.ValidTo;
        assignment.ChangeReason = request.ChangeReason?.Trim();
        assignment.CorrelationId = request.CorrelationId?.Trim();
        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        await Assignments.UpdateAsync(assignment, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class SoftDeleteTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<SoftDeleteTerritoryResourceAssignmentCommand, Response<bool>>
{
    public SoftDeleteTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<bool>> Handle(SoftDeleteTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken, draftOnly: true);
        if (model is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var assignment = await Assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Response<bool>.Fail("Resource assignment not found.", 404);
        }

        // Pack §10: terminating an assignment is NOT deleting it. Only a still-mutable (proposed) row may be removed;
        // anything that has taken effect must be ended so the history survives.
        var statusMeta = await References.GetValueMetadataAsync(
            TerritoryReferenceSets.TerritoryAssignmentStatus, assignment.Status, cancellationToken);
        if (!Common.ReferenceValidation.ReferenceMetadata.TryGetBool(
                statusMeta, TerritoryAssignmentMetadataKeys.AllowsMutation, out var mutable) || !mutable)
        {
            return Response<bool>.Fail(
                $"Only a '{TerritoryResourceAssignmentValidation.DefaultStatus}' resource assignment can be deleted; "
                + "end the assignment instead so the history is preserved.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        assignment.IsDeleted = true;
        assignment.DeletedAt = now;
        assignment.UpdatedAt = now;
        assignment.CorrelationId = request.CorrelationId?.Trim();
        await Assignments.UpdateAsync(assignment, cancellationToken);

        return Response<bool>.Success(true);
    }
}

public sealed class EndTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<EndTerritoryResourceAssignmentCommand, Response<bool>>
{
    public EndTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<bool>> Handle(EndTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var assignment = await Assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Response<bool>.Fail("Resource assignment not found.", 404);
        }

        if (string.Equals(assignment.Status, TerritoryResourceAssignmentValidation.EndedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("This resource assignment is already ended.", 409);
        }

        var operational = string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase);
        if (operational && (request.EndDate is null || string.IsNullOrWhiteSpace(request.Reason)))
        {
            return Response<bool>.Fail("Effective end date and reason are required on an active model.", 400);
        }

        var endedCheck = await References.ValidateValueAsync(
            TerritoryReferenceSets.TerritoryAssignmentStatus, TerritoryResourceAssignmentValidation.EndedStatus, cancellationToken);
        if (endedCheck != Common.ReferenceValidation.ReferenceValidationStatus.Valid)
        {
            return Response<bool>.Fail("Assignment status reference values are not published.", 400);
        }

        // Default to "now", except for an assignment that has not started yet: ending a future assignment collapses
        // it to its own start date, which records that it never took effect. Defaulting to a bare "now" would be
        // earlier than ValidFrom and would reject the very case a planner most often wants — cancelling a plan that
        // is still ahead of them. An EXPLICIT date before ValidFrom is still a mistake and is rejected.
        var now = DateTimeOffset.UtcNow;
        var endDate = request.EndDate ?? (now < assignment.ValidFrom ? assignment.ValidFrom : now);
        if (endDate < assignment.ValidFrom)
        {
            return Response<bool>.Fail("End date cannot be earlier than the assignment ValidFrom.", 400);
        }

        assignment.Status = TerritoryResourceAssignmentValidation.EndedStatus;
        assignment.ValidTo = endDate;
        assignment.ChangeReason = request.Reason?.Trim() ?? assignment.ChangeReason;
        assignment.CorrelationId = request.CorrelationId?.Trim();
        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        await Assignments.UpdateAsync(assignment, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ReplaceTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<ReplaceTerritoryResourceAssignmentCommand, Response<Guid>>
{
    public ReplaceTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<Guid>> Handle(ReplaceTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<Guid>.Fail("Tenant context is required.", 400);

        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null) return Response<Guid>.Fail(error!, status);
        if (!string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            return Response<Guid>.Fail("Replacement is only available on an active territory model.", 409);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Response<Guid>.Fail("Replacement reason is required.", 400);
        if (ValidateResource(request.Resource) is { } resourceError)
            return Response<Guid>.Fail(resourceError.Message, resourceError.StatusCode);

        var source = await Assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (source is null) return Response<Guid>.Fail("Resource assignment not found.", 404);
        if (!string.Equals(source.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            return Response<Guid>.Fail("Only an active assignment can be replaced.", 409);
        if (request.EffectiveDate < source.ValidFrom)
            return Response<Guid>.Fail("Effective date cannot be earlier than the source ValidFrom.", 400);

        var node = source.TerritoryId is { } nodeId
            ? await Nodes.GetByIdAsync(tenantId, request.ModelId, nodeId, cancellationToken) : null;
        var (resolution, validationError) = await TerritoryResourceAssignmentValidation.ResolveAsync(
            References, model, node, source.TerritoryId, request.PositionId, request.PositionCode, request.PositionTitle,
            request.PositionType, request.PositionSourceSystem, source.CoverageScope,
            source.BusinessScopes.Select(s => s.ScopeCode).ToList(), source.IsPrimary, source.AssignmentSource,
            request.Reason, request.EffectiveDate, source.ValidTo, cancellationToken, operational: true);
        if (resolution is null) return Response<Guid>.Fail(validationError!.Message, validationError.StatusCode);

        var created = new TerritoryResourceAssignment
        {
            TenantId = tenantId,
            ModelId = request.ModelId,
            TerritoryId = source.TerritoryId,
            Resource = ToRef(request.Resource!),
            Position = resolution.Position,
            PositionId = resolution.Position.PositionId,
            PositionCode = resolution.Position.PositionCode,
            PositionName = resolution.Position.PositionTitle,
            CoverageScope = resolution.CoverageScope,
            BusinessScopes = resolution.BusinessScopes,
            Status = TerritoryResourceAssignmentValidation.ActiveStatus,
            AssignmentSource = resolution.AssignmentSource,
            IsPrimary = source.IsPrimary,
            ValidFrom = request.EffectiveDate,
            ValidTo = source.ValidTo,
            ChangeReason = request.Reason.Trim(),
            CorrelationId = request.CorrelationId?.Trim(),
            ReplacedAssignmentId = source.Id,
            ReplacementReason = request.Reason.Trim(),
            PreviousPositionCode = source.EffectivePositionCode,
            NewPositionCode = resolution.Position.PositionCode
        };

        var existing = await Assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodeMap = (await Nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var conflict = TerritoryResourceConflictEngine.FindBlockingConflict(
            created, existing.Where(a => a.Id != source.Id).ToList(), nodeMap,
            IsOverride(created.AssignmentSource));
        if (conflict is not null) return Response<Guid>.Fail(conflict.Message, 409);

        var ended = Copy(source);
        ended.Status = TerritoryResourceAssignmentValidation.EndedStatus;
        ended.ValidTo = request.EffectiveDate;
        ended.ChangeReason = request.Reason.Trim();
        ended.CorrelationId = request.CorrelationId?.Trim();
        ended.ReplacementAssignmentId = created.Id;
        ended.ReplacementReason = request.Reason.Trim();
        ended.PreviousPositionCode = source.EffectivePositionCode;
        ended.NewPositionCode = created.EffectivePositionCode;
        ended.UpdatedAt = DateTimeOffset.UtcNow;

        await Assignments.CommitLifecycleTransitionAsync(ended, created, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }
}

public sealed class TransferTerritoryResourceAssignmentHandler
    : TerritoryResourceAssignmentHandlerBase, IRequestHandler<TransferTerritoryResourceAssignmentCommand, Response<Guid>>
{
    public TransferTerritoryResourceAssignmentHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryNodeRepository nodes,
        ITerritoryResourceAssignmentRepository assignments, ITerritoryReferenceValidator references)
        : base(tenant, models, nodes, assignments, references) { }

    public async Task<Response<Guid>> Handle(TransferTerritoryResourceAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
            return Response<Guid>.Fail("Tenant context is required.", 400);
        var (model, error, status) = await LoadMutableModelAsync(tenantId, request.ModelId, cancellationToken);
        if (model is null) return Response<Guid>.Fail(error!, status);
        if (!string.Equals(model.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            return Response<Guid>.Fail("Transfer is only available on an active territory model.", 409);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Response<Guid>.Fail("Transfer reason is required.", 400);

        var source = await Assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, cancellationToken);
        if (source is null) return Response<Guid>.Fail("Resource assignment not found.", 404);
        if (!string.Equals(source.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            return Response<Guid>.Fail("Only an active assignment can be transferred.", 409);
        if (request.EffectiveDate < source.ValidFrom)
            return Response<Guid>.Fail("Effective date cannot be earlier than the source ValidFrom.", 400);

        var node = request.TargetTerritoryId is { } nodeId
            ? await Nodes.GetByIdAsync(tenantId, request.ModelId, nodeId, cancellationToken) : null;
        var position = source.Position;
        var (resolution, validationError) = await TerritoryResourceAssignmentValidation.ResolveAsync(
            References, model, node, request.TargetTerritoryId, position.PositionId ?? source.PositionId,
            source.EffectivePositionCode, source.EffectivePositionTitle, position.PositionType, position.SourceSystem,
            request.CoverageScope ?? source.CoverageScope,
            request.BusinessUnitScopeCodes ?? source.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            source.IsPrimary, source.AssignmentSource, request.Reason, request.EffectiveDate, source.ValidTo,
            cancellationToken, operational: true);
        if (resolution is null) return Response<Guid>.Fail(validationError!.Message, validationError.StatusCode);

        var created = new TerritoryResourceAssignment
        {
            TenantId = tenantId,
            ModelId = request.ModelId,
            TerritoryId = request.TargetTerritoryId,
            Resource = Copy(source).Resource,
            Position = resolution.Position,
            PositionId = resolution.Position.PositionId,
            PositionCode = resolution.Position.PositionCode,
            PositionName = resolution.Position.PositionTitle,
            CoverageScope = resolution.CoverageScope,
            BusinessScopes = resolution.BusinessScopes,
            Status = TerritoryResourceAssignmentValidation.ActiveStatus,
            AssignmentSource = resolution.AssignmentSource,
            IsPrimary = source.IsPrimary,
            ValidFrom = request.EffectiveDate,
            ValidTo = source.ValidTo,
            ChangeReason = request.Reason.Trim(),
            CorrelationId = request.CorrelationId?.Trim(),
            TransferFromAssignmentId = source.Id,
            TransferReason = request.Reason.Trim(),
            PreviousPositionCode = source.EffectivePositionCode,
            NewPositionCode = resolution.Position.PositionCode
        };

        var existing = await Assignments.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var nodeMap = (await Nodes.ListByModelAsync(tenantId, request.ModelId, cancellationToken)).ToDictionary(n => n.Id);
        var conflict = TerritoryResourceConflictEngine.FindBlockingConflict(
            created, existing.Where(a => a.Id != source.Id).ToList(), nodeMap,
            IsOverride(created.AssignmentSource));
        if (conflict is not null) return Response<Guid>.Fail(conflict.Message, 409);

        var ended = Copy(source);
        ended.Status = TerritoryResourceAssignmentValidation.EndedStatus;
        ended.ValidTo = request.EffectiveDate;
        ended.ChangeReason = request.Reason.Trim();
        ended.CorrelationId = request.CorrelationId?.Trim();
        ended.TransferToAssignmentId = created.Id;
        ended.TransferReason = request.Reason.Trim();
        ended.UpdatedAt = DateTimeOffset.UtcNow;

        await Assignments.CommitLifecycleTransitionAsync(ended, created, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }
}
