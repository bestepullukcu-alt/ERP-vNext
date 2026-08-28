using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Closes a play (draft or active to archived). This is the only removal there is: no endpoint in this FU deletes a
/// document, and the archived row keeps its bindings so a past play stays explainable.
/// </summary>
public sealed class ArchiveStrategyTemplateHandler : IRequestHandler<ArchiveStrategyTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IStrategyTemplateRepository _templates;

    public ArchiveStrategyTemplateHandler(
        ITenantContext tenant, IActorContext actor, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
    }

    public async Task<Response<bool>> Handle(
        ArchiveStrategyTemplateCommand request, CancellationToken cancellationToken)
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
            return Response<bool>.Fail("The strategy template is already archived.", 409);
        }

        var expectedVersion = request.ExpectedVersion ?? template.Version;
        if (expectedVersion != template.Version)
        {
            return Response<bool>.Fail(
                "The strategy template changed since it was loaded. Reload and try again.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        template.TemplateStatus = StrategyTemplateStatuses.Archived;
        template.ArchivedAt = now;
        template.ArchivedBy = _actor.ActorName;
        template.UpdatedAt = now;
        template.UpdatedBy = _actor.ActorName;

        var replaced = await _templates.ReplaceAsync(template, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The strategy template changed since it was loaded. Reload and try again.", 409);
    }
}
