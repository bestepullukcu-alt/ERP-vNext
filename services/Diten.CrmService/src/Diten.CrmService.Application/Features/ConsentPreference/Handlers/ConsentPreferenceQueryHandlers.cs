using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.ConsentPreference.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PrefType = Diten.CrmService.Domain.Entities.PreferenceType;

namespace Diten.CrmService.Application.Features.ConsentPreference.Handlers;

public sealed class ListConsentRecordsHandler
    : IRequestHandler<ListConsentRecordsQuery, Response<ConsentRecordListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConsentRecordRepository _repository;

    public ListConsentRecordsHandler(ITenantContext tenant, IConsentRecordRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<ConsentRecordListDto>> Handle(
        ListConsentRecordsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConsentRecordListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ConsentRecord> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SubjectType))
        {
            var subjectType = ConsentSubjectType.Normalize(request.SubjectType);
            rows = rows.Where(r => r.SubjectType == subjectType);
        }

        if (request.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(r => r.SubjectId == subjectId);
        }

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            var channel = ConsentChannel.Normalize(request.Channel);
            rows = rows.Where(r => r.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Purpose))
        {
            var purpose = ConsentPurpose.Normalize(request.Purpose);
            rows = rows.Where(r => r.Purpose == purpose);
        }

        if (!string.IsNullOrWhiteSpace(request.ConsentStatus))
        {
            var status = ConsentStatuses.Normalize(request.ConsentStatus);
            rows = rows.Where(r => r.ConsentStatus == status);
        }

        // Archived rows are readable history and are included unless the caller opts out.
        if (!request.IncludeArchived)
        {
            rows = rows.Where(r => !r.IsArchived());
        }

        var items = rows.Select(ConsentPreferenceMapper.ToDto).ToList();
        return Response<ConsentRecordListDto>.Success(new ConsentRecordListDto(items, items.Count));
    }
}

public sealed class GetConsentRecordHandler : IRequestHandler<GetConsentRecordQuery, Response<ConsentRecordDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConsentRecordRepository _repository;

    public GetConsentRecordHandler(ITenantContext tenant, IConsentRecordRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<ConsentRecordDto>> Handle(
        GetConsentRecordQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConsentRecordDto>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.ConsentId, cancellationToken);
        return record is null
            ? Response<ConsentRecordDto>.Fail("Consent record not found.", 404)
            : Response<ConsentRecordDto>.Success(ConsentPreferenceMapper.ToDto(record));
    }
}

/// <summary>
/// Read-only evaluation handler. Validates the question dimensions, then delegates to the single
/// <see cref="IConsentPreferenceEvaluator"/> seam. This handler performs NO writes.
/// </summary>
public sealed class EvaluateConsentHandler : IRequestHandler<EvaluateConsentQuery, Response<ConsentEvaluationResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IConsentPreferenceEvaluator _evaluator;

    public EvaluateConsentHandler(ITenantContext tenant, IConsentPreferenceEvaluator evaluator)
    {
        _tenant = tenant;
        _evaluator = evaluator;
    }

    public async Task<Response<ConsentEvaluationResult>> Handle(
        EvaluateConsentQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } _)
        {
            return Response<ConsentEvaluationResult>.Fail("Tenant context is required.", 400);
        }

        // An unrecognized channel/purpose/subject is a malformed QUESTION, not an "unknown" answer — it is rejected
        // with 400 so a caller can never smuggle a typo past the evaluator and read the unknown result as harmless.
        if (ConsentPreferenceValidation.ValidateSubjectType(request.SubjectType) is { } subjectTypeError)
        {
            return Response<ConsentEvaluationResult>.Fail(subjectTypeError, 400);
        }

        if (ConsentPreferenceValidation.ValidateSubjectId(request.SubjectId) is { } subjectIdError)
        {
            return Response<ConsentEvaluationResult>.Fail(subjectIdError, 400);
        }

        if (ConsentPreferenceValidation.ValidateConsentChannel(request.Channel) is { } channelError)
        {
            return Response<ConsentEvaluationResult>.Fail(channelError, 400);
        }

        if (ConsentPreferenceValidation.ValidatePurpose(request.Purpose) is { } purposeError)
        {
            return Response<ConsentEvaluationResult>.Fail(purposeError, 400);
        }

        if (ConsentPreferenceValidation.ValidateScope(request.ScopeType, request.ScopeId) is { } scopeError)
        {
            return Response<ConsentEvaluationResult>.Fail(scopeError, 400);
        }

        var result = await _evaluator.EvaluateAsync(
            new ConsentEvaluationRequest(
                request.SubjectType, request.SubjectId, request.Channel, request.Purpose, request.EffectiveAt,
                request.ScopeType, request.ScopeId, request.IncludeDiagnostics),
            cancellationToken);

        return Response<ConsentEvaluationResult>.Success(result);
    }
}

public sealed class ListPreferenceRecordsHandler
    : IRequestHandler<ListPreferenceRecordsQuery, Response<PreferenceRecordListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPreferenceRecordRepository _repository;

    public ListPreferenceRecordsHandler(ITenantContext tenant, IPreferenceRecordRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<PreferenceRecordListDto>> Handle(
        ListPreferenceRecordsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PreferenceRecordListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<PreferenceRecord> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SubjectType))
        {
            var subjectType = ConsentSubjectType.Normalize(request.SubjectType);
            rows = rows.Where(r => r.SubjectType == subjectType);
        }

        if (request.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(r => r.SubjectId == subjectId);
        }

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            var channel = PreferenceChannel.Normalize(request.Channel);
            rows = rows.Where(r => r.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.PreferenceType))
        {
            var preferenceType = PrefType.Normalize(request.PreferenceType);
            rows = rows.Where(r => r.PreferenceType == preferenceType);
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(r => !r.IsArchived());
        }

        var items = rows.Select(ConsentPreferenceMapper.ToDto).ToList();
        return Response<PreferenceRecordListDto>.Success(new PreferenceRecordListDto(items, items.Count));
    }
}

public sealed class GetPreferenceRecordHandler : IRequestHandler<GetPreferenceRecordQuery, Response<PreferenceRecordDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPreferenceRecordRepository _repository;

    public GetPreferenceRecordHandler(ITenantContext tenant, IPreferenceRecordRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<PreferenceRecordDto>> Handle(
        GetPreferenceRecordQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PreferenceRecordDto>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.PreferenceId, cancellationToken);
        return record is null
            ? Response<PreferenceRecordDto>.Fail("Preference record not found.", 404)
            : Response<PreferenceRecordDto>.Success(ConsentPreferenceMapper.ToDto(record));
    }
}
