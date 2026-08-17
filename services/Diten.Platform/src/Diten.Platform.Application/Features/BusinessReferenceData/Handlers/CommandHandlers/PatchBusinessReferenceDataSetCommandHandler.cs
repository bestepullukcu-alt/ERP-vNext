using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class PatchBusinessReferenceDataSetCommandHandler : IRequestHandler<PatchBusinessReferenceDataSetCommand, Response<BusinessReferenceDataSetDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public PatchBusinessReferenceDataSetCommandHandler(IBusinessReferenceDataStewardshipRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<BusinessReferenceDataSetDetailModel>> Handle(PatchBusinessReferenceDataSetCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetSetByIdAsync(request.SetId, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("reference_data_set_not_found");
        }

        if (!string.IsNullOrWhiteSpace(request.SetCode) && !string.Equals(entity.SetCode, request.SetCode.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("immutable_field:setCode");
        }

        if (!string.IsNullOrWhiteSpace(request.ScopeType) && !string.Equals(entity.ScopeType, request.ScopeType.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("immutable_field:scopeType");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<BusinessReferenceDataSetStatus>(request.Status, true, out var parsedStatus))
            {
                throw new InvalidOperationException("invalid_set_status");
            }

            entity.Status = parsedStatus;
        }

        entity.LastCorrelationId = request.CorrelationId;
        entity.UpdatedBy = _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system";

        var updated = await _repository.UpdateSetAsync(entity, request.RowVersion, ct);
        if (!updated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return Response<BusinessReferenceDataSetDetailModel>.Success(new BusinessReferenceDataSetDetailModel(
            entity.BusinessReferenceDataSetId,
            entity.SetCode,
            entity.Name,
            entity.ScopeType,
            entity.Description,
            entity.Status.ToString(),
            entity.ActiveDraftVersionId,
            entity.PublishedVersionId,
            entity.RowVersion,
            entity.CreatedAt,
            entity.UpdatedAt));
    }
}
