using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdateOrganizationUnitCommandHandler : IRequestHandler<UpdateOrganizationUnitCommand, Response<NoContent>>
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly IOrganizationReportingGraphRepository _graph;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;

    public UpdateOrganizationUnitCommandHandler(
        IOrganizationUnitRepository repository,
        IOrganizationReportingGraphRepository graph,
        ILegalEntityReferenceValidator legalEntityValidator)
    {
        _repository = repository;
        _graph = graph;
        _legalEntityValidator = legalEntityValidator;
    }

    public async Task<Response<NoContent>> Handle(UpdateOrganizationUnitCommand request, CancellationToken ct)
    {
        /*
         * ⚠ THE TOKEN IS READ FIRST — BEFORE THE ENTITY, BEFORE THE VALIDATION. That ordering is the whole
         * guarantee. It marks the version of the graph this validation is about to reason over; if anything
         * re-parents anything between here and our write, the compare-and-set below fails and we answer 409
         * rather than committing a decision that was made about a graph that no longer exists.
         *
         * Reading it AFTER the validation would be the classic mistake: the window it is supposed to close is
         * exactly the validation itself.
         */
        var structureToken = await _graph.ReadStructureTokenAsync(ct);

        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        if (entity.IsArchived)
        {
            return Response<NoContent>.Fail("Archived Organization Unit cannot be mutated.", 409);
        }

        var canonicalCode = OrganizationCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<NoContent>.Fail("Organization Unit code is required.", 400);
        }

        if (await _repository.ExistsByCodeAsync(canonicalCode, request.Id, ct))
        {
            return Response<NoContent>.Fail("Organization Unit code already exists.", 409);
        }

        var legalEntity = await _legalEntityValidator.ValidateAsync(request.Request.LegalEntityId, ct);
        if (!legalEntity.IsSuccessful || legalEntity.Data?.Referenceable != true)
        {
            return Response<NoContent>.Fail("Legal Entity is not referenceable.", 404);
        }

        var functionalParent = request.Request.ParentOrganizationUnitId;
        var administrativeParent = request.Request.AdministrativeParentOrganizationUnitId;

        if (functionalParent.HasValue)
        {
            var parentCheck = await ValidateParentAsync(request.Id, functionalParent.Value, request.Request.LegalEntityId, ct);
            if (!parentCheck.IsSuccessful)
            {
                return parentCheck;
            }
        }

        if (administrativeParent.HasValue)
        {
            var administrativeCheck = await ValidateParentAsync(request.Id, administrativeParent.Value, request.Request.LegalEntityId, ct);
            if (!administrativeCheck.IsSuccessful)
            {
                return administrativeCheck;
            }
        }

        /*
         * ⚠ THE COMBINED-GRAPH PASS IS SEPARATE FROM THE TWO ABOVE, AND IT IS NOT REDUNDANT. Each line alone
         * can be perfectly acyclic while the union of the two is not: A-functional->B together with
         * B-administrative->A is the smallest case. Nothing in a per-line walk can see it.
         */
        if (functionalParent.HasValue || administrativeParent.HasValue)
        {
            var combined = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
                _graph, request.Id, functionalParent, administrativeParent, ct);
            if (!combined.IsSuccessful)
            {
                return combined;
            }
        }

        /*
         * ⚠ WON BEFORE THE WRITE, NOT AFTER. Two concurrent re-parentings are each acyclic against the graph
         * they read and cyclic together; per-document CAS cannot see that, because neither document is stale.
         * The advance below is the point where the two processes actually meet: both read the same token, one
         * advance lands, and the loser is told to look again at a graph that now contains the winner's edge.
         *
         * Only a LINE CHANGE contends for it. Renaming a unit does not touch the graph and must not be made
         * to fail because somebody elsewhere re-parented something.
         */
        var lineChanged = entity.ParentOrganizationUnitId != functionalParent
            || entity.AdministrativeParentOrganizationUnitId != administrativeParent;

        /*
         * ⚠ THE SEPARATION §14 ASKS FOR, ENFORCED WHERE IT CANNOT BE ROUTED AROUND. Two endpoints send this
         * command; only the reporting-line one sets the flag. It lives in the handler rather than the
         * controller because a third caller will exist one day, and a rule that lives in one controller action
         * is a rule the next action does not have.
         *
         * ⚠ AND IT RUNS AFTER VALIDATION, DELIBERATELY. An invalid parent — a cycle, a cross-legal-entity
         * move, a unit that is not there — keeps answering exactly what it answered before FU02 existed.
         * Putting the 403 first would have changed the reply to inputs that were already being refused, which
         * is a contract change nobody asked for; and it discloses nothing, since anyone who can update a unit
         * can already read the tree.
         *
         * A line sent UNCHANGED is not a change. That is what keeps an ordinary editor — which round-trips the
         * whole record — working under `…update` alone.
         */
        if (lineChanged && !request.AllowReportingLineChange)
        {
            return Response<NoContent>.Fail(
                "Changing a reporting line needs platform.organization-units.reporting-line.update; "
                + "use the reporting-lines endpoint.",
                403);
        }

        if (lineChanged && !await _graph.TryAdvanceStructureTokenAsync(structureToken, ct))
        {
            return Response<NoContent>.Fail(
                "The organization structure changed while this reporting line was being validated. "
                + "Re-read the unit and try again.",
                409);
        }

        entity.Code = canonicalCode;
        entity.Name = request.Request.Name.Trim();
        entity.LegalEntityId = request.Request.LegalEntityId;
        entity.ParentOrganizationUnitId = functionalParent;
        entity.AdministrativeParentOrganizationUnitId = administrativeParent;
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }

    private async Task<Response<NoContent>> ValidateParentAsync(Guid currentId, Guid parentId, Guid legalEntityId, CancellationToken ct)
    {
        if (currentId == parentId)
        {
            return Response<NoContent>.Fail("Organization Unit cannot be its own parent.", 409);
        }

        var parent = await _repository.GetByIdAsync(parentId, ct);
        if (parent == null || parent.IsArchived)
        {
            return Response<NoContent>.Fail("Parent Organization Unit not found.", 404);
        }

        if (parent.LegalEntityId != legalEntityId)
        {
            return Response<NoContent>.Fail("Parent Organization Unit must belong to the same Legal Entity.", 409);
        }

        return await OrganizationUnitCycleGuard.EnsureNoCycleAsync(_graph, currentId, parentId, ct);
    }
}

/// <summary>
/// MOD-0288-FU02 §14 — the reporting-line write path.
///
/// <para>⚠ IT DELEGATES RATHER THAN REVALIDATES. Every rule about a parent — existence, tenant, legal entity,
/// per-line and combined cycles, depth, the structure-token guard — already lives in
/// <see cref="UpdateOrganizationUnitCommandHandler"/>, and a second copy of them here is how one of the two
/// stops being maintained. What this handler owns is the OTHER half of the separation: everything that is not
/// a reporting line is read back from storage, so this permission cannot rename a unit, move it to another
/// legal entity or retire it.</para>
/// </summary>
public sealed class UpdateOrganizationUnitReportingLinesCommandHandler
    : IRequestHandler<UpdateOrganizationUnitReportingLinesCommand, Response<NoContent>>
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly UpdateOrganizationUnitCommandHandler _inner;

    public UpdateOrganizationUnitReportingLinesCommandHandler(
        IOrganizationUnitRepository repository,
        IOrganizationReportingGraphRepository graph,
        ILegalEntityReferenceValidator legalEntityValidator)
    {
        _repository = repository;
        _inner = new UpdateOrganizationUnitCommandHandler(repository, graph, legalEntityValidator);
    }

    public async Task<Response<NoContent>> Handle(
        UpdateOrganizationUnitReportingLinesCommand request,
        CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        // Every field but the two lines comes from what is already stored. That is the enforcement, not a
        // convention: there is no path from this request to any other property.
        var rehydrated = new OrganizationUnitRequest(
            entity.Code,
            entity.Name,
            entity.LegalEntityId,
            request.Request.ParentOrganizationUnitId,
            entity.OrgUnitType.ToString(),
            entity.ManagerPositionId,
            entity.Description,
            entity.Status.ToString(),
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.LocationCode,
            entity.CostCenterCode,
            request.Request.AdministrativeParentOrganizationUnitId);

        return await _inner.Handle(
            new UpdateOrganizationUnitCommand(request.Id, rehydrated, AllowReportingLineChange: true), ct);
    }
}
