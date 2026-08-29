using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Handlers;

/// <summary>MOD-0162 FU04 embedded-step write handlers (D2). Each mutates the SAME path document and rides the path's
/// optimistic <see cref="EntityBase.Version"/> token — a step write bumps the path Version. In-array StepOrder/StepCode
/// uniqueness has no DB index, so the handler is the only defence (§4.5).</summary>
public sealed class AddKnowledgePathStepHandler : IRequestHandler<AddKnowledgePathStepCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public AddKnowledgePathStepHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgeContentRepository contents, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<Response<Guid>> Handle(AddKnowledgePathStepCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<Guid>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<Guid>.Fail("An archived path cannot be modified.", 409);
        }

        if (KnowledgePathWrite.EnsureNotFrozen(path) is { } frozen)
        {
            return Response<Guid>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<Guid>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        // V-S20 — document growth guard.
        if (path.Steps.Count >= KnowledgePathLimits.MaxStepsPerPath)
        {
            return Response<Guid>.Fail(
                $"A path cannot hold more than {KnowledgePathLimits.MaxStepsPerPath} steps.", 400);
        }

        var scalarError = KnowledgePathValidation.ValidateStepCode(request.StepCode)
            ?? KnowledgePathValidation.ValidateStepTitle(request.StepTitle)
            ?? KnowledgePathValidation.ValidateStepType(request.StepType)
            ?? KnowledgePathValidation.ValidateCompletionRule(request.CompletionRule)
            ?? KnowledgePathValidation.ValidateVersionPin(request.VersionPinPolicy)
            ?? KnowledgePathValidation.ValidateNotes(request.Notes)
            ?? KnowledgePathValidation.ValidateDuration(request.CompletionRule, request.EstimatedDurationMinutes)
            ?? KnowledgePathValidation.ValidateBranchShape(request.BranchConditions);
        if (scalarError is not null)
        {
            return Response<Guid>.Fail(scalarError, 400);
        }

        // V-S03/S04 — unique among active steps (handler is the only defence).
        if (KnowledgePathValidation.ValidateStepUniqueness(path, request.StepOrder, request.StepCode, null) is { } dup)
        {
            return Response<Guid>.Fail(dup, 409);
        }

        var (content, contentError, contentCode) = await KnowledgePathWrite.ValidateStepContentAsync(
            _contents, tenantId, path, request.ContentId, request.CompletionRule, cancellationToken);
        if (contentError is not null)
        {
            return Response<Guid>.Fail(contentError, contentCode);
        }

        if (await KnowledgePathWrite.ValidateConceptNodeAsync(
                _nodes, tenantId, request.ConceptNodeId, cancellationToken) is { } nodeError)
        {
            return Response<Guid>.Fail(nodeError, 400);
        }

        var newStepId = Guid.NewGuid();

        // V-S09/S10 — prerequisite direction/cycle/required-optional (self excluded via newStepId).
        if (KnowledgePathValidation.ValidatePrerequisite(
                path, request.PrerequisiteStepId, newStepId, request.StepOrder, request.IsRequired) is { } prereq)
        {
            return Response<Guid>.Fail(prereq, 400);
        }

        // V-S14 — branch targets must reference a step in the same path (this new step included).
        var stepIds = path.Steps.Select(s => s.StepId).Append(newStepId).ToHashSet();
        if (KnowledgePathValidation.ValidateBranchTargets(request.BranchConditions, stepIds) is { } branchError)
        {
            return Response<Guid>.Fail(branchError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        var step = new KnowledgePathStep
        {
            StepId = newStepId,
            StepOrder = request.StepOrder,
            StepCode = request.StepCode.Trim(),
            StepTitle = request.StepTitle.Trim(),
            StepType = KnowledgePathStepTypes.Normalize(request.StepType),
            ContentId = request.ContentId,
            ContentCode = content!.ContentCode,
            VersionPinPolicy = KnowledgePathVersionPin.Normalize(request.VersionPinPolicy),
            IsRequired = request.IsRequired,
            CompletionRule = KnowledgePathCompletionRules.Normalize(request.CompletionRule),
            PrerequisiteStepId = request.PrerequisiteStepId,
            ConceptNodeId = request.ConceptNodeId,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            Notes = KnowledgePathValidation.Trim(request.Notes),
            BranchConditions = KnowledgePathWrite.MapBranchConditions(request.BranchConditions),
            StepStatus = KnowledgePathStepStatuses.Active,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        path.Steps.Add(step);
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        var ok = await _paths.ReplaceAsync(path, path.Version, cancellationToken);
        return ok
            ? Response<Guid>.Success(newStepId, 201)
            : Response<Guid>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}

public sealed class UpdateKnowledgePathStepHandler : IRequestHandler<UpdateKnowledgePathStepCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public UpdateKnowledgePathStepHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgeContentRepository contents, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<Response<bool>> Handle(
        UpdateKnowledgePathStepCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Fail("An archived path cannot be modified.", 409);
        }

        if (KnowledgePathWrite.EnsureNotFrozen(path) is { } frozen)
        {
            return Response<bool>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        var step = path.Steps.FirstOrDefault(s => s.StepId == request.StepId);
        if (step is null)
        {
            return Response<bool>.Fail("Step not found in this path.", 404);
        }

        if (step.IsArchived())
        {
            return Response<bool>.Fail("An archived step cannot be updated.", 409);
        }

        var scalarError = KnowledgePathValidation.ValidateStepCode(request.StepCode)
            ?? KnowledgePathValidation.ValidateStepTitle(request.StepTitle)
            ?? KnowledgePathValidation.ValidateStepType(request.StepType)
            ?? KnowledgePathValidation.ValidateCompletionRule(request.CompletionRule)
            ?? KnowledgePathValidation.ValidateVersionPin(request.VersionPinPolicy)
            ?? KnowledgePathValidation.ValidateNotes(request.Notes)
            ?? KnowledgePathValidation.ValidateDuration(request.CompletionRule, request.EstimatedDurationMinutes)
            ?? KnowledgePathValidation.ValidateBranchShape(request.BranchConditions);
        if (scalarError is not null)
        {
            return Response<bool>.Fail(scalarError, 400);
        }

        if (KnowledgePathValidation.ValidateStepUniqueness(
                path, request.StepOrder, request.StepCode, request.StepId) is { } dup)
        {
            return Response<bool>.Fail(dup, 409);
        }

        // V-S07 dirty-check — re-validate content only when ContentId actually changed; also re-check when the
        // completion rule flips to assessment-passed (the quiz constraint depends on the referenced content).
        var contentChanged = request.ContentId != step.ContentId;
        var completionChanged = !string.Equals(
            KnowledgePathCompletionRules.Normalize(request.CompletionRule),
            step.CompletionRule, StringComparison.Ordinal);
        var contentCode = step.ContentCode;
        if (contentChanged || completionChanged)
        {
            var (content, contentError, code) = await KnowledgePathWrite.ValidateStepContentAsync(
                _contents, tenantId, path, request.ContentId, request.CompletionRule, cancellationToken);
            if (contentError is not null)
            {
                return Response<bool>.Fail(contentError, code);
            }

            contentCode = content!.ContentCode;
        }

        // V-S13 — re-validate concept node only when it changed (dirty-check spirit).
        if (request.ConceptNodeId != step.ConceptNodeId
            && await KnowledgePathWrite.ValidateConceptNodeAsync(
                _nodes, tenantId, request.ConceptNodeId, cancellationToken) is { } nodeError)
        {
            return Response<bool>.Fail(nodeError, 400);
        }

        if (KnowledgePathValidation.ValidatePrerequisite(
                path, request.PrerequisiteStepId, request.StepId, request.StepOrder, request.IsRequired) is { } prereq)
        {
            return Response<bool>.Fail(prereq, 400);
        }

        var stepIds = path.Steps.Select(s => s.StepId).ToHashSet();
        if (KnowledgePathValidation.ValidateBranchTargets(request.BranchConditions, stepIds) is { } branchError)
        {
            return Response<bool>.Fail(branchError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        step.StepOrder = request.StepOrder;
        step.StepCode = request.StepCode.Trim();
        step.StepTitle = request.StepTitle.Trim();
        step.StepType = KnowledgePathStepTypes.Normalize(request.StepType);
        step.ContentId = request.ContentId;
        step.ContentCode = contentCode;
        step.VersionPinPolicy = KnowledgePathVersionPin.Normalize(request.VersionPinPolicy);
        step.IsRequired = request.IsRequired;
        step.CompletionRule = KnowledgePathCompletionRules.Normalize(request.CompletionRule);
        step.PrerequisiteStepId = request.PrerequisiteStepId;
        step.ConceptNodeId = request.ConceptNodeId;
        step.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        step.Notes = KnowledgePathValidation.Trim(request.Notes);
        step.BranchConditions = KnowledgePathWrite.MapBranchConditions(request.BranchConditions);
        step.UpdatedAt = now;
        step.UpdatedBy = _actor.ActorName;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        var ok = await _paths.ReplaceAsync(path, path.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}

public sealed class ArchiveKnowledgePathStepHandler
    : IRequestHandler<ArchiveKnowledgePathStepCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public ArchiveKnowledgePathStepHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<bool>> Handle(
        ArchiveKnowledgePathStepCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Fail("An archived path cannot be modified.", 409);
        }

        if (KnowledgePathWrite.EnsureNotFrozen(path) is { } frozen)
        {
            return Response<bool>.Fail(frozen, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        var step = path.Steps.FirstOrDefault(s => s.StepId == request.StepId);
        if (step is null)
        {
            return Response<bool>.Fail("Step not found in this path.", 404);
        }

        if (step.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // V-S17 — an active step that is another active step's prerequisite cannot be archived (dangling prerequisite).
        var dependent = path.Steps.FirstOrDefault(s =>
            !s.IsArchived() && s.StepId != step.StepId && s.PrerequisiteStepId == step.StepId);
        if (dependent is not null)
        {
            return Response<bool>.Fail(
                $"Step '{step.StepCode}' is the prerequisite of active step '{dependent.StepCode}' and cannot be " +
                "archived (dangling prerequisite).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        step.StepStatus = KnowledgePathStepStatuses.Archived;
        step.ArchivedAt = now;
        step.ArchivedBy = _actor.ActorName;
        step.UpdatedAt = now;
        step.UpdatedBy = _actor.ActorName;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        var ok = await _paths.ReplaceAsync(path, path.Version, cancellationToken);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}
