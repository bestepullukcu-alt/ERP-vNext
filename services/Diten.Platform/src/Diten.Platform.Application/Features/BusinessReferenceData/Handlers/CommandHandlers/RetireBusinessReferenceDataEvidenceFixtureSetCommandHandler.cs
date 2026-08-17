using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class RetireBusinessReferenceDataEvidenceFixtureSetCommandHandler : IRequestHandler<RetireBusinessReferenceDataEvidenceFixtureSetCommand, Response<BusinessReferenceDataEvidenceFixtureRetireModel>>
{
    private const string FixturePrefix = "FX15F";
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public RetireBusinessReferenceDataEvidenceFixtureSetCommandHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataEvidenceFixtureRetireModel>> Handle(RetireBusinessReferenceDataEvidenceFixtureSetCommand request, CancellationToken ct)
    {
        var fixtureCode = NormalizeRequired(request.FixtureCode, "fixture_code_required").ToUpperInvariant();
        if (!fixtureCode.StartsWith(FixturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("fixture_code_not_allowed");
        }

        var set = await _repository.GetSetByIdAsync(request.SetId, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");
        if (!set.SetCode.StartsWith(fixtureCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("fixture_set_mismatch");
        }

        if (set.Status != BusinessReferenceDataSetStatus.Retired)
        {
            var expectedRowVersion = request.ExpectedRowVersion ?? set.RowVersion;
            set.Status = BusinessReferenceDataSetStatus.Retired;
            set.UpdatedBy = request.ActorId;
            set.LastCorrelationId = request.CorrelationId;
            var updated = await _repository.UpdateSetAsync(set, expectedRowVersion, ct);
            if (!updated)
            {
                throw new InvalidOperationException("concurrency_conflict");
            }
        }

        return Response<BusinessReferenceDataEvidenceFixtureRetireModel>.Success(new BusinessReferenceDataEvidenceFixtureRetireModel(
            fixtureCode,
            set.BusinessReferenceDataSetId,
            set.SetCode,
            set.Status.ToString(),
            set.RowVersion));
    }

    private static string NormalizeRequired(string? value, string errorCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorCode);
        }

        return normalized;
    }
}
