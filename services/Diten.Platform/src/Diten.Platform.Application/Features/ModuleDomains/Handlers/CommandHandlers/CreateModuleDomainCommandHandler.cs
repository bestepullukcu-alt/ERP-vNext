using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.ModuleDomains.Handlers.CommandHandlers;

public sealed class CreateModuleDomainCommandHandler : IRequestHandler<CreateModuleDomainCommand, Response<Guid>>
{
    private readonly IModuleDomainRepository _repository;

    public CreateModuleDomainCommandHandler(IModuleDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateModuleDomainCommand request, CancellationToken ct)
    {
        // FIX-DOMAIN-DEDUP — canonical domain code = the SHARED normalizer used by every creation path (manifest
        // self-registration + seed): trim + UPPERCASE + drop every separator. This is the same shape as CodeKey,
        // so two logical-same codes in different formats can never both persist (unique index on CodeKey).
        var canonicalCode = ModuleTaxonomyCanonicalizer.NormalizeKey(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("Domain code is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Request.DisplayName))
        {
            return Response<Guid>.Fail("Display name is required.", 400);
        }

        // ExistsByCodeAsync yalnız canlı (IsDeleted=false) kayıtlara bakar; silinen kod tekrar create edilebilsin diye böyledir.
        if (await _repository.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail(ModuleDomainErrorCodes.DomainCodeInUse, 409);
        }

        var item = new ModuleDomain
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
            return Response<Guid>.Fail(ModuleDomainErrorCodes.DomainCodeInUse, 409);
        }

        return Response<Guid>.Success(item.Id, 201);
    }
}
