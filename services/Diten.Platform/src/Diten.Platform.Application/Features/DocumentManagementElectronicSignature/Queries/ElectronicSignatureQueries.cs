using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Queries;

// MOD-0029-FU23 — electronic signature read queries (tenant-scoped; no side effects).

public sealed record GetSignaturePoliciesQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<SignaturePolicyModel>>>;

public sealed record GetSignaturePolicyByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<SignaturePolicyModel>>;

public sealed record GetSignatureRequestsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<SignatureRequestModel>>>;

public sealed record GetSignatureRequestByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<SignatureRequestModel>>;

public sealed record GetSignaturesQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<SignatureRecordModel>>>;

public sealed record GetSignatureByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<SignatureRecordModel>>;

/// <summary>The full attestation history for one subject — invalidated records included.</summary>
public sealed record GetSignaturesBySubjectQuery(string SubjectType, Guid SubjectId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<SignatureRecordModel>>>;

public sealed record GetSignedObjectFingerprintsQuery(string SubjectType, Guid SubjectId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<SignedObjectFingerprintModel>>>;
