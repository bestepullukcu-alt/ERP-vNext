using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class CreateModulePageActionDescriptorCommandHandler
    : IRequestHandler<CreateModulePageActionDescriptorCommand, Response<Guid>>
{
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly IModulePageActionDescriptorRepository _repository;
    private readonly ICatalogPermissionSyncService _catalogPermissionSyncService;

    public CreateModulePageActionDescriptorCommandHandler(
        IModulePageDescriptorRepository pageRepository,
        IModulePageActionDescriptorRepository repository,
        ICatalogPermissionSyncService catalogPermissionSyncService)
    {
        _pageRepository = pageRepository;
        _repository = repository;
        _catalogPermissionSyncService = catalogPermissionSyncService;
    }

    public async Task<Response<Guid>> Handle(CreateModulePageActionDescriptorCommand request, CancellationToken ct)
    {
        var page = await _pageRepository.GetByIdAsync(request.PageDescriptorId, ct);
        if (page is null)
        {
            return Response<Guid>.Fail("Module page descriptor not found.", 404);
        }

        var actionCode = ModulePageDescriptorNormalizer.NormalizePageCode(request.Request.ActionCode);
        if (await _repository.ExistsByActionCodeAsync(page.Id, actionCode, null, ct))
        {
            return Response<Guid>.Fail("ActionCode already exists for this page.", 409);
        }

        var descriptor = new ModulePageActionDescriptor
        {
            TenantId = page.TenantId,
            PageDescriptorId = page.Id,
            ModuleCode = page.ModuleCode,
            PageCode = page.PageCode,
            ActionCode = actionCode,
            DisplayName = request.Request.DisplayName.Trim(),
            PermissionKey = ModulePageDescriptorNormalizer.NormalizePermission(request.Request.PermissionKey),
            ActionType = Enum.Parse<ModulePageActionType>(request.Request.ActionType, ignoreCase: false),
            SortOrder = request.Request.SortOrder ?? 0,
            IsDangerous = request.Request.IsDangerous,
            IsToolbarAction = request.Request.IsToolbarAction,
            IsRowAction = request.Request.IsRowAction,
            Status = Enum.Parse<ModulePageActionStatus>(request.Request.Status, ignoreCase: false),
            Description = ModulePageDescriptorNormalizer.NormalizeOptional(request.Request.Description)
        };

        await _repository.CreateAsync(descriptor, ct);

        // Declarative permission sync: surface this action's permission into the AuthService catalogue.
        // Best-effort — the service never throws, so a sync failure cannot roll back the catalog save.
        await _catalogPermissionSyncService.SyncPermissionAsync(descriptor.PermissionKey, descriptor.DisplayName, ct);

        return Response<Guid>.Success(descriptor.Id, 201);
    }
}
