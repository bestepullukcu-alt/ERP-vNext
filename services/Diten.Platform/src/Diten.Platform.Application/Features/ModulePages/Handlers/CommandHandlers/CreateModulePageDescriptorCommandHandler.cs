using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class CreateModulePageDescriptorCommandHandler : IRequestHandler<CreateModulePageDescriptorCommand, Response<Guid>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public CreateModulePageDescriptorCommandHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateModulePageDescriptorCommand request, CancellationToken ct)
    {
        var moduleCode = ModulePageDescriptorNormalizer.NormalizeModuleCode(request.Request.ModuleCode);
        if (!await _repository.ModuleExistsAsync(moduleCode, ct))
        {
            return Response<Guid>.Fail("Parent module catalog item not found.", 404);
        }

        var pageCode = ModulePageDescriptorNormalizer.NormalizePageCode(request.Request.PageCode);
        var routePath = ModulePageDescriptorNormalizer.NormalizeRoutePath(request.Request.RoutePath);

        if (await _repository.ExistsByPageCodeAsync(moduleCode, pageCode, null, ct))
        {
            return Response<Guid>.Fail("Sayfa kodu bu modülde zaten kayıtlı.", 409);
        }

        if (await _repository.ExistsByRoutePathAsync(moduleCode, routePath, null, ct))
        {
            return Response<Guid>.Fail("Rota yolu bu modülde zaten kayıtlı.", 409);
        }

        var descriptor = new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = moduleCode,
            PageCode = pageCode,
            DisplayName = request.Request.DisplayName.Trim(),
            RoutePath = routePath,
            RequiredPermission = ModulePageDescriptorNormalizer.NormalizeOptionalPermission(request.Request.RequiredPermission),
            ParentPageCode = ModulePageDescriptorNormalizer.NormalizeOptionalPageCode(request.Request.ParentPageCode),
            IsNavigationVisible = request.Request.IsNavigationVisible ?? true,
            PageType = Enum.Parse<ModulePageType>(request.Request.PageType, ignoreCase: false),
            Status = Enum.Parse<ModulePageStatus>(request.Request.Status, ignoreCase: false),
            SortOrder = request.Request.SortOrder ?? 0,
            Description = ModulePageDescriptorNormalizer.NormalizeOptional(request.Request.Description)
        };

        await _repository.CreateAsync(descriptor, ct);
        return Response<Guid>.Success(descriptor.Id, 201);
    }
}
