using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.CommandHandlers;

/// <summary>
/// postMovement (POST /api/inventory/movements). The posting path itself lives in
/// <see cref="InternalMovementPostingService"/> (extracted for MOD-0175, pack MOD-0175 §5 Q1 — behaviour unchanged): the
/// API caller posts in the resolved company as itself.
/// </summary>
public sealed class PostMovementHandler : IRequestHandler<PostMovementCommand, Response<MovementResponseDto>>
{
    private readonly InternalMovementPostingService _posting;
    private readonly ITenantContext _tenantContext;

    public PostMovementHandler(InternalMovementPostingService posting, ITenantContext tenantContext)
    {
        _posting = posting;
        _tenantContext = tenantContext;
    }

    public Task<Response<MovementResponseDto>> Handle(PostMovementCommand request, CancellationToken cancellationToken)
        => _posting.PostAsync(request, _tenantContext.LegalEntityId, _tenantContext.UserId, cancellationToken);
}
