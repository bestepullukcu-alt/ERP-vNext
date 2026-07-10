using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreateOrganizationUnitCommandHandler : IRequestHandler<CreateOrganizationUnitCommand, Response<Guid>>
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;
    private readonly ITenantContext _tenantContext;

    public CreateOrganizationUnitCommandHandler(
        IOrganizationUnitRepository repository,
        ILegalEntityReferenceValidator legalEntityValidator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _legalEntityValidator = legalEntityValidator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateOrganizationUnitCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var canonicalCode = OrganizationCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("Organization Unit code is required.", 400);
        }

        if (await _repository.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail("Organization Unit code already exists.", 409);
        }

        var legalEntity = await _legalEntityValidator.ValidateAsync(request.Request.LegalEntityId, ct);
        if (!legalEntity.IsSuccessful || legalEntity.Data?.Referenceable != true)
        {
            return Response<Guid>.Fail("Legal Entity is not referenceable.", 404);
        }

        if (request.Request.ParentOrganizationUnitId.HasValue)
        {
            var parentCheck = await ValidateParentAsync(null, request.Request.ParentOrganizationUnitId.Value, request.Request.LegalEntityId, ct);
            if (!parentCheck.IsSuccessful)
            {
                return Response<Guid>.Fail(parentCheck.Errors, parentCheck.StatusCode);
            }
        }

        var entity = new OrganizationUnit
        {
            TenantId = tenantId,
            Code = canonicalCode,
            Name = request.Request.Name.Trim(),
            LegalEntityId = request.Request.LegalEntityId,
            ParentOrganizationUnitId = request.Request.ParentOrganizationUnitId
        };
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private async Task<Response<NoContent>> ValidateParentAsync(Guid? currentId, Guid parentId, Guid legalEntityId, CancellationToken ct)
    {
        if (currentId.HasValue && currentId.Value == parentId)
        {
            return Response<NoContent>.Fail("Organization Unit cannot be its own parent.", 409);
        }

        var parent = await _repository.GetByIdAsync(parentId, ct);
        if (parent == null || parent.IsArchived)
        {
            return Response<NoContent>.Fail("Parent Organization Unit not found.", 404);
        }

        if (parent.LegalEntityId != legalEntityId)
        {
            return Response<NoContent>.Fail("Parent Organization Unit must belong to the same Legal Entity.", 409);
        }

        return await OrganizationUnitCycleGuard.EnsureNoCycleAsync(_repository, currentId, parentId, ct);
    }
}
