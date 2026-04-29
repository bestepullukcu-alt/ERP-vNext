using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateModuleCatalogItemCommandHandler : IRequestHandler<CreateModuleCatalogItemCommand, Response<Guid>>
{
    private readonly IModuleCatalogRepository _repository;

    public CreateModuleCatalogItemCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        if (await _repository.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail("ModuleCode already exists.", 409);
        }

        var item = new ModuleCatalogItem
        {
            ModuleCode = canonicalCode,
            ModuleName = request.Request.ModuleName.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            Domain = request.Request.Domain.Trim(),
            Service = request.Request.Service.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Request.Category) ? null : request.Request.Category.Trim(),
            Status = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false),
            ModuleVersion = request.Request.ModuleVersion.Trim(),
            IsCoreModule = request.Request.IsCoreModule,
            IsTenantAssignable = request.Request.IsTenantAssignable,
            SortOrder = request.Request.SortOrder ?? 0
        };

        await _repository.CreateAsync(item, ct);
        return Response<Guid>.Success(item.Id, 201);
    }
}
