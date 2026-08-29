using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Queries;

// MOD-0029-FU21 — GDocP correction trail read queries (tenant-scoped; no side effects).

public sealed record GetGDocPCorrectionPoliciesQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<GDocPCorrectionPolicyModel>>>;

public sealed record GetGDocPCorrectionPolicyByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<GDocPCorrectionPolicyModel>>;

public sealed record GetGDocPCorrectionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<GDocPCorrectionRecordModel>>>;

public sealed record GetGDocPCorrectionByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<GDocPCorrectionRecordModel>>;

/// <summary>The correction history of one regulated record — the question an auditor actually asks.</summary>
public sealed record GetGDocPCorrectionsBySubjectQuery(string SubjectType, Guid SubjectId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<GDocPCorrectionRecordModel>>>;

public sealed record GetGDocPCorrectionReviewsQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<GDocPCorrectionReviewModel>>>;

public sealed record GetPendingGDocPCorrectionReviewsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<GDocPCorrectionRecordModel>>>;
