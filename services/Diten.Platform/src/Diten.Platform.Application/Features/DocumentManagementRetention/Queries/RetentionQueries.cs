using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Queries;

// MOD-0029-FU15 — retention / legal hold / disposition read queries (tenant-scoped; no side effects).

public sealed record GetRetentionPoliciesQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<RetentionPolicyModel>>>;

public sealed record GetRetentionPolicyByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<RetentionPolicyModel>>;

public sealed record GetRetentionSubjectQuery(string SubjectType, Guid SubjectId, string CorrelationId)
    : IRequest<Response<RetentionSubjectModel>>;

/// <summary>Subjects past retention with no active hold — disposition REQUEST candidates, never a purge list.</summary>
public sealed record GetEligibleRetentionSubjectsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<RetentionSubjectModel>>>;

public sealed record GetLegalHoldsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<LegalHoldModel>>>;

public sealed record GetLegalHoldByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<LegalHoldModel>>;

public sealed record GetLegalHoldSubjectsQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<LegalHoldSubjectModel>>>;

public sealed record GetDispositionRequestsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DispositionRequestModel>>>;

public sealed record GetDispositionRequestByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>;
