using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class CreateBusinessReferenceDataSetCommandHandler : IRequestHandler<CreateBusinessReferenceDataSetCommand, Response<BusinessReferenceDataSetDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateBusinessReferenceDataSetCommandHandler(IBusinessReferenceDataStewardshipRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<BusinessReferenceDataSetDetailModel>> Handle(CreateBusinessReferenceDataSetCommand request, CancellationToken ct)
    {
        var normalizedCode = request.SetCode.Trim();
        var existing = await _repository.GetSetByCodeAsync(normalizedCode, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException("duplicate_set_code");
        }

        var status = ParseSetStatus(request.Status);
        var entity = new BusinessReferenceDataSet
        {
            TenantId = Guid.Empty, // overwritten by TenantRepository.CreateAsync based on tenant context
            BusinessReferenceDataSetId = Guid.NewGuid(),
            SetCode = normalizedCode,
            Name = request.Name.Trim(),
            ScopeType = request.ScopeType.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = status,
            LastCorrelationId = request.CorrelationId,
            CreatedBy = _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system"
        };

        await _repository.CreateSetAsync(entity, ct);
        return Response<BusinessReferenceDataSetDetailModel>.Success(ToSetModel(entity), 201);
    }

    private static BusinessReferenceDataSetStatus ParseSetStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BusinessReferenceDataSetStatus.Draft;
        }

        if (!Enum.TryParse<BusinessReferenceDataSetStatus>(raw, true, out var parsed))
        {
            throw new InvalidOperationException("invalid_set_status");
        }

        return parsed;
    }

    private static BusinessReferenceDataSetDetailModel ToSetModel(BusinessReferenceDataSet entity)
    {
        return new BusinessReferenceDataSetDetailModel(
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
            entity.UpdatedAt);
    }
}
