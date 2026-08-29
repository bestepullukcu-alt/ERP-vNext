using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Puts a play live and FREEZES its bindings. Two things are re-proven at this exact moment, because the world may have
/// moved since the draft was written: every bound segment must still be bindable AND must now be <c>active</c>, and the
/// bound content must still be published.
/// <para>If any of that fails, the answer is a 409 and <b>no freeze stamp is written</b> — a half-activated play (frozen
/// but not live) would be unfixable without a new version.</para>
/// <para>Activating also supersedes the predecessor version of the same lineage, so exactly one version of a lineage is
/// ever live while the older one stays readable.</para>
/// </summary>
public sealed class ActivateStrategyTemplateHandler : IRequestHandler<ActivateStrategyTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IStrategyTemplateRepository _templates;
    private readonly StrategyTemplateBindingValidator _bindings;

    public ActivateStrategyTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IStrategyTemplateRepository templates,
        StrategyTemplateBindingValidator bindings)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _bindings = bindings;
    }

    public async Task<Response<bool>> Handle(
        ActivateStrategyTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Response<bool>.Fail("Strategy template not found.", 404);
        }

        if (template.IsArchived())
        {
            return Response<bool>.Fail("An archived strategy template cannot be activated.", 409);
        }

        if (template.IsActive())
        {
            return Response<bool>.Fail("The strategy template is already active.", 409);
        }

        var expectedVersion = request.ExpectedVersion ?? template.Version;
        if (expectedVersion != template.Version)
        {
            return Response<bool>.Fail(
                "The strategy template changed since it was loaded. Reload and try again.", 409);
        }

        // Re-proof at go-live, with the stricter segment rule. Nothing is stamped before this passes.
        var bindingFailure = await _bindings.ValidateAsync(
            tenantId, template, requireActiveSegments: true, cancellationToken);
        if (bindingFailure is not null)
        {
            return Response<bool>.Fail(
                StrategyTemplateWriteGuards.ToErrors(bindingFailure),
                bindingFailure.StatusCode == 400 ? 409 : bindingFailure.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        template.TemplateStatus = StrategyTemplateStatuses.Active;
        template.BindingsFrozenAt = now;
        template.ActivatedAt = now;
        template.ActivatedBy = _actor.ActorName;
        template.UpdatedAt = now;
        template.UpdatedBy = _actor.ActorName;

        var replaced = await _templates.ReplaceAsync(template, expectedVersion, cancellationToken);
        if (!replaced)
        {
            return Response<bool>.Fail(
                "The strategy template changed since it was loaded. Reload and try again.", 409);
        }

        await SupersedePredecessorAsync(tenantId, template, now, cancellationToken);
        return Response<bool>.Success(true);
    }

    /// <summary>
    /// Marks the previous live version of the same lineage as superseded. It is a SEPARATE single-document write on
    /// purpose: if it fails, the new version is still correctly live and the old one is merely still marked active —
    /// visible and repairable — whereas a multi-document transaction on a standalone dev Mongo would fail the whole
    /// activation.
    /// </summary>
    private async Task SupersedePredecessorAsync(
        Guid tenantId,
        Domain.Entities.StrategyTemplate activated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var lineage = await _templates.ListByLineageAsync(tenantId, activated.VersionLineageId, cancellationToken);
        var predecessor = lineage
            .Where(t => t.Id != activated.Id
                        && t.TemplateVersion < activated.TemplateVersion
                        && !t.IsArchived()
                        && t.SupersededByTemplateId is null)
            .OrderByDescending(t => t.TemplateVersion)
            .FirstOrDefault();

        if (predecessor is null)
        {
            return;
        }

        predecessor.SupersededByTemplateId = activated.Id;
        predecessor.UpdatedAt = now;
        predecessor.UpdatedBy = _actor.ActorName;
        await _templates.ReplaceAsync(predecessor, predecessor.Version, cancellationToken);
    }
}
