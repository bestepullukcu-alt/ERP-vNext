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
    private readonly Services.IModuleTaxonomyResolver _taxonomyResolver;

    public CreateModuleCatalogItemCommandHandler(
        IModuleCatalogRepository repository,
        Services.IModuleTaxonomyResolver taxonomyResolver)
    {
        _repository = repository;
        _taxonomyResolver = taxonomyResolver;
    }

    public async Task<Response<Guid>> Handle(CreateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        // GetByCodeAsync yalnız canlı (IsDeleted=false) kayıtlara bakar; silinen kod tekrar create edilebilsin diye böyledir.
        var existing = await _repository.GetByCodeAsync(canonicalCode, ct);
        if (existing is not null)
        {
            // MC-4 — a self-registered (code-owned) module cannot be re-created manually; surface a distinct reason.
            return existing.Origin == ModuleCatalogOrigin.SelfRegistered
                ? Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409)
                : Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409);
        }

        // FIX-DOMAIN-SERVICE-CANONICAL — defensive: even though the form now submits lookup Codes, resolve again so a
        // free-typed DisplayName/enum-name can never be persisted as the Domain/Service.
        var domain = await _taxonomyResolver.ResolveDomainCodeAsync(request.Request.Domain, ct);
        var service = await _taxonomyResolver.ResolveServiceCodeAsync(request.Request.Service, ct);

        var item = new ModuleCatalogItem
        {
            ModuleCode = canonicalCode,
            ModuleName = request.Request.ModuleName.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            Domain = domain,
            Service = service,
            Status = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false),
            ModuleVersion = request.Request.ModuleVersion.Trim(),
            IsCoreModule = request.Request.IsCoreModule,
            IsTenantAssignable = request.Request.IsTenantAssignable,
            SortOrder = request.Request.SortOrder ?? 0,
            Origin = ModuleCatalogOrigin.Manual // MC-4 — operator-added
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
