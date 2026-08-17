using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class UpdateHrisMappingProfileHandler : IRequestHandler<UpdateHrisMappingProfileCommand, Response<NoContent>>
{
    private readonly IHrisSourceRepository _repository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly IPositionRepository _positionRepository;

    public UpdateHrisMappingProfileHandler(
        IHrisSourceRepository repository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository)
    {
        _repository = repository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
    }

    public async Task<Response<NoContent>> Handle(UpdateHrisMappingProfileCommand request, CancellationToken ct)
    {
        var source = await _repository.GetSourceProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<NoContent>.Fail("HRIS source not found.", 404);
        }

        var existing = await _repository.GetActiveMappingProfileAsync(source.Id, ct);
        var mappingProfile = existing ?? new HrisMappingProfile
        {
            TenantId = source.TenantId,
            SourceProfileId = source.Id,
            Code = string.Empty,
            DisplayName = string.Empty,
            MappingProfileVersion = string.Empty,
            ExternalSchemaReference = string.Empty
        };

        mappingProfile.Code = HrisSourceCodeNormalizer.Normalize(request.Request.Code);
        mappingProfile.DisplayName = request.Request.DisplayName.Trim();
        mappingProfile.MappingProfileVersion = request.Request.MappingProfileVersion.Trim();
        mappingProfile.ExternalSchemaReference = request.Request.ExternalSchemaReference.Trim();
        mappingProfile.EffectiveFrom = request.Request.EffectiveFrom;
        mappingProfile.IsActive = request.Request.IsActive;

        var referenceValidation = await ValidateInternalReferencesAsync(request.Request.IdentifierMaps, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<NoContent>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        await _repository.UpsertMappingProfileAsync(mappingProfile, ct);

        var maps = request.Request.IdentifierMaps
            .Select(x => new HrisExternalIdentifierMap
            {
                TenantId = source.TenantId,
                SourceProfileId = source.Id,
                ExternalObjectType = x.ExternalObjectType,
                ExternalObjectId = x.ExternalObjectId.Trim(),
                InternalReferenceType = x.InternalReferenceType,
                InternalReferenceId = x.InternalReferenceId,
                MappingState = x.MappingState,
                ProvenanceHash = string.IsNullOrWhiteSpace(x.ProvenanceHash) ? null : x.ProvenanceHash.Trim()
            })
            .ToList();

        await _repository.ReplaceIdentifierMapsAsync(source.Id, maps, ct);
        source.MappingProfileId = mappingProfile.Id;
        await _repository.UpdateSourceProfileAsync(source, ct);

        return Response<NoContent>.Success(204);
    }

    private async Task<Response<NoContent>> ValidateInternalReferencesAsync(
        IReadOnlyList<HrisExternalIdentifierMapRequest> identifierMaps,
        CancellationToken ct)
    {
        foreach (var map in identifierMaps)
        {
            if (map.MappingState == HrisMappingState.Mapped && !map.InternalReferenceId.HasValue)
            {
                return Response<NoContent>.Fail("Mapped HRIS identifier requires an internal MOD-0288 reference.", 409);
            }

            if (!map.InternalReferenceId.HasValue)
            {
                continue;
            }

            var exists = map.InternalReferenceType switch
            {
                HrisInternalReferenceType.OrganizationUnit => await _organizationUnitRepository.GetByIdAsync(map.InternalReferenceId.Value, ct) != null,
                HrisInternalReferenceType.Position => await _positionRepository.GetByIdAsync(map.InternalReferenceId.Value, ct) != null,
                HrisInternalReferenceType.Person => false,
                _ => false
            };

            if (!exists)
            {
                return Response<NoContent>.Fail("Internal MOD-0288 reference not found for current tenant.", 404);
            }
        }

        return Response<NoContent>.Success(204);
    }
}
