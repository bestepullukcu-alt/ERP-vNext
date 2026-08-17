using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class CreateBusinessReferenceDataVersionCommandHandler : IRequestHandler<CreateBusinessReferenceDataVersionCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateBusinessReferenceDataVersionCommandHandler(IBusinessReferenceDataStewardshipRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(CreateBusinessReferenceDataVersionCommand request, CancellationToken ct)
    {
        var set = await _repository.GetSetByIdAsync(request.SetId, ct);
        if (set is null)
        {
            throw new KeyNotFoundException("reference_data_set_not_found");
        }

        var hasDraft = await _repository.HasActiveDraftVersionAsync(request.SetId, ct);
        if (hasDraft)
        {
            throw new InvalidOperationException("active_draft_exists");
        }

        var nextVersionNo = await _repository.GetNextVersionNumberAsync(request.SetId, ct);
        var version = new BusinessReferenceDataVersion
        {
            TenantId = set.TenantId,
            BusinessReferenceDataVersionId = Guid.NewGuid(),
            BusinessReferenceDataSetId = set.BusinessReferenceDataSetId,
            VersionNumber = nextVersionNo,
            Status = BusinessReferenceDataVersionStatus.Draft,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsImmutable = false,
            SourceVersionId = request.SourceVersionId ?? set.PublishedVersionId,
            TargetDraftVersionId = null,
            CopyActor = _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system",
            CopiedAt = DateTimeOffset.UtcNow,
            LastCorrelationId = request.CorrelationId,
            CreatedBy = _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system",
            BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Draft,
            BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.NotStarted,
            IsEditable = true
        };

        await _repository.CreateVersionAsync(version, ct);

        var previousSetRowVersion = set.RowVersion;
        set.ActiveDraftVersionId = version.BusinessReferenceDataVersionId;
        set.UpdatedBy = _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system";
        set.LastCorrelationId = request.CorrelationId;

        var setUpdated = await _repository.UpdateSetAsync(set, previousSetRowVersion, ct);
        if (!setUpdated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return Response<BusinessReferenceDataVersionDetailModel>.Success(BusinessReferenceDataModelMapper.ToVersionDetail(version), 201);
    }
}
