using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.ModuleDomains.Handlers.CommandHandlers;

public sealed class UpdateModuleDomainCommandHandler : IRequestHandler<UpdateModuleDomainCommand, Response<NoContent>>
{
    private readonly IModuleDomainRepository _repository;

    public UpdateModuleDomainCommandHandler(IModuleDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(UpdateModuleDomainCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module domain not found.", 404);
        }

        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<NoContent>.Fail("Domain code is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Request.DisplayName))
        {
            return Response<NoContent>.Fail("Display name is required.", 400);
        }

        if (await _repository.ExistsByCodeAsync(canonicalCode, item.Id, ct))
        {
            return Response<NoContent>.Fail(ModuleDomainErrorCodes.DomainCodeInUse, 409);
        }

        item.Code = canonicalCode;
        item.DisplayName = request.Request.DisplayName.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        item.SortOrder = request.Request.SortOrder ?? 0;
        item.IsActive = request.Request.IsActive;

        try
        {
            await _repository.UpdateAsync(item, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Response<NoContent>.Fail(ModuleDomainErrorCodes.DomainCodeInUse, 409);
        }

        return Response<NoContent>.Success(204);
    }
}
