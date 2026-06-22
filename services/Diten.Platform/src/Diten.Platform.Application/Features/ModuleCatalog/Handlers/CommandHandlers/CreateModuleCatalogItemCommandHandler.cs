using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using MongoDB.Driver;

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
        // ExistsByCodeAsync yalnız canlı (IsDeleted=false) kayıtlara bakar; silinen kod tekrar create edilebilsin diye böyledir.
        if (await _repository.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409);
        }

        var item = new ModuleCatalogItem
        {
            ModuleCode = canonicalCode,
            ModuleName = request.Request.ModuleName.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            Domain = request.Request.Domain.Trim(),
            Service = request.Request.Service.Trim(),
            Status = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false),
            ModuleVersion = request.Request.ModuleVersion.Trim(),
            IsCoreModule = request.Request.IsCoreModule,
            IsTenantAssignable = request.Request.IsTenantAssignable,
            SortOrder = request.Request.SortOrder ?? 0
        };

        try
        {
            await _repository.CreateAsync(item, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Defense-in-depth: partial unique index + canlı pre-check normalde buraya düşürmez (yalnız yarış durumunda).
            return Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409);
        }

        return Response<Guid>.Success(item.Id, 201);
    }
}
