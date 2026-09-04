using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using MongoDB.Driver;
using Diten.Platform.Application.Features.GlobalApplicability;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateModuleCatalogItemCommandHandler : IRequestHandler<CreateModuleCatalogItemCommand, Response<Guid>>
{
    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly Services.IModuleTaxonomyResolver _taxonomyResolver;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public CreateModuleCatalogItemCommandHandler(
        ITransactionalModuleCatalogRepository repository,
        Services.IModuleTaxonomyResolver taxonomyResolver, IGlobalApplicabilityTransactionCoordinator transaction,
        IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _taxonomyResolver = taxonomyResolver;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<Guid>> Handle(CreateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        // GetByCodeAsync yalnız canlı (IsDeleted=false) kayıtlara bakar; silinen kod tekrar create edilebilsin diye böyledir.
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
            Icon = string.IsNullOrWhiteSpace(request.Request.Icon) ? null : request.Request.Icon.Trim(), // FIX-MODULE-ICON
            Origin = ModuleCatalogOrigin.Manual // MC-4 — operator-added
        };

        return await _transaction.ExecuteAsync(
            new(nameof(CreateModuleCatalogItemCommand), AuditOperation.Create, "ModuleCatalogItem", item.Id),
            async (session, transactionCt) =>
            {
                var existing = await _repository.GetByCodeAsync(session, canonicalCode, transactionCt);
                if (existing is not null)
                    return new GlobalApplicabilityMutation<Response<Guid>>(existing.Origin == ModuleCatalogOrigin.SelfRegistered
                        ? Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409)
                        : Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409), false);
                try { await _repository.CreateAsync(session, item, transactionCt); }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                { return new GlobalApplicabilityMutation<Response<Guid>>(Response<Guid>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409), false); }
                return new GlobalApplicabilityMutation<Response<Guid>>(Response<Guid>.Success(item.Id, 201), true,
                    (s, version, token) => _state.UpsertModuleCatalogAsync(s, item, version, token));
            }, ct);
    }
}
