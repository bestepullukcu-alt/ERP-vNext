using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleServices.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.ModuleServices.Handlers.CommandHandlers;

public sealed class CreateModuleServiceCommandHandler : IRequestHandler<CreateModuleServiceCommand, Response<Guid>>
{
    private readonly IModuleServiceRepository _repository;

    public CreateModuleServiceCommandHandler(IModuleServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateModuleServiceCommand request, CancellationToken ct)
    {
        // Reuse the catalog code normalizer: trim + UPPERCASE + collapse separators.
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("Service code is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Request.DisplayName))
        {
            return Response<Guid>.Fail("Display name is required.", 400);
        }

        // ExistsByCodeAsync yalnız canlı (IsDeleted=false) kayıtlara bakar; silinen kod tekrar create edilebilsin diye böyledir.
        if (await _repository.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail(ModuleServiceErrorCodes.ServiceCodeInUse, 409);
        }

        var item = new ModuleService
        {
            Code = canonicalCode,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            SortOrder = request.Request.SortOrder ?? 0,
            IsActive = request.Request.IsActive
        };

        try
        {
            await _repository.CreateAsync(item, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Defense-in-depth: partial unique index + canlı pre-check normalde buraya düşürmez (yalnız yarış durumunda).
            return Response<Guid>.Fail(ModuleServiceErrorCodes.ServiceCodeInUse, 409);
        }

        return Response<Guid>.Success(item.Id, 201);
    }
}
