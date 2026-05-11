using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class AddTenantModuleEntitlementCommandHandler : IRequestHandler<AddTenantModuleEntitlementCommand, Response<Guid>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;

    public AddTenantModuleEntitlementCommandHandler(ITenantModuleEntitlementRepository repository, IModuleCatalogRepository moduleRepository)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
    }

    public async Task<Response<Guid>> Handle(AddTenantModuleEntitlementCommand request, CancellationToken ct)
    {
        var moduleCode = TenantModuleEntitlementCommandSupport.NormalizeModuleCode(request.Request.ModuleCode);
        var moduleValidation = await TenantModuleEntitlementCommandSupport.ValidateModuleAsync(_moduleRepository, moduleCode, ct);
        if (!moduleValidation.IsValid)
        {
            return Response<Guid>.Fail(moduleValidation.Error!, moduleValidation.StatusCode);
        }

        var duplicate = await TenantModuleEntitlementCommandSupport.ValidateDuplicateAsync(
            _repository,
            request.TenantId,
            moduleCode,
            request.Request.Source,
            null,
            ct);
        if (!duplicate.IsValid)
        {
            return Response<Guid>.Fail(duplicate.Error!, duplicate.StatusCode);
        }

        var entitlement = new TenantModuleEntitlement
        {
            TenantId = request.TenantId,
            ModuleCode = moduleCode,
            Source = request.Request.Source,
            IsEnabled = request.Request.IsEnabled,
            ExpiryDateUtc = request.Request.ExpiryDateUtc,
            Reason = request.Request.Reason
        };

        await _repository.CreateAsync(entitlement, ct);
        return Response<Guid>.Success(entitlement.Id, 201);
    }
}
