using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdateOrganizationUnitCommandHandler : IRequestHandler<UpdateOrganizationUnitCommand, Response<NoContent>>
{
    private readonly IOrganizationUnitRepository _repository;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;

    public UpdateOrganizationUnitCommandHandler(IOrganizationUnitRepository repository, ILegalEntityReferenceValidator legalEntityValidator)
    {
        _repository = repository;
        _legalEntityValidator = legalEntityValidator;
    }

    public async Task<Response<NoContent>> Handle(UpdateOrganizationUnitCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        if (entity.IsArchived)
        {
            return Response<NoContent>.Fail("Archived Organization Unit cannot be mutated.", 409);
        }

        var canonicalCode = OrganizationCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<NoContent>.Fail("Organization Unit code is required.", 400);
        }

        if (await _repository.ExistsByCodeAsync(canonicalCode, request.Id, ct))
        {
            return Response<NoContent>.Fail("Organization Unit code already exists.", 409);
        }

        var legalEntity = await _legalEntityValidator.ValidateAsync(request.Request.LegalEntityId, ct);
        if (!legalEntity.IsSuccessful || legalEntity.Data?.Referenceable != true)
        {
            return Response<NoContent>.Fail("Legal Entity is not referenceable.", 404);
        }

        if (request.Request.ParentOrganizationUnitId.HasValue)
        {
            var parentCheck = await ValidateParentAsync(request.Id, request.Request.ParentOrganizationUnitId.Value, request.Request.LegalEntityId, ct);
            if (!parentCheck.IsSuccessful)
            {
                return parentCheck;
            }
        }

        entity.Code = canonicalCode;
        entity.Name = request.Request.Name.Trim();
        entity.LegalEntityId = request.Request.LegalEntityId;
        entity.ParentOrganizationUnitId = request.Request.ParentOrganizationUnitId;

        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }

    private async Task<Response<NoContent>> ValidateParentAsync(Guid currentId, Guid parentId, Guid legalEntityId, CancellationToken ct)
    {
        if (currentId == parentId)
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
