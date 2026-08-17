using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ESignature.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.QueryHandlers;

public sealed class GetSignatureEnvelopeByIdHandler
    : IRequestHandler<GetSignatureEnvelopeByIdQuery, Response<SignatureEnvelopeDetailDto>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSignatureEnvelopeByIdHandler(IESignatureRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SignatureEnvelopeDetailDto>> Handle(
        GetSignatureEnvelopeByIdQuery query,
        CancellationToken ct)
    {
        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, query.TargetTenantId);
        var envelope = await _repository.GetEnvelopeAsync(query.EnvelopeId, ct);
        if (envelope is null)
        {
            return Response<SignatureEnvelopeDetailDto>.Fail("Signature envelope was not found.", 404);
        }

        var participants = await _repository.GetParticipantsAsync(envelope.Id, ct);
        var attestations = await _repository.GetAttestationsAsync(envelope.Id, ct);
        var verifications = await _repository.GetVerificationsAsync(envelope.Id, ct);
        return Response<SignatureEnvelopeDetailDto>.Success(new SignatureEnvelopeDetailDto(
            envelope.Id,
            envelope.TenantId,
            envelope.EnvelopeNumber,
            envelope.SubjectType,
            envelope.SubjectId,
            envelope.SubjectVersion,
            envelope.DocumentArtifactId,
            envelope.SourceArtifactHash,
            envelope.PolicyCode,
            envelope.Status.ToString(),
            envelope.RequestedBy,
            envelope.RequestedAt,
            envelope.CompletedAt,
            envelope.CancelledAt,
            envelope.CancelReason,
            envelope.CorrelationId,
            envelope.Version,
            participants.Select(ESignatureMappings.ToDto).ToArray(),
            attestations.Select(ESignatureMappings.ToDto).ToArray(),
            verifications.Select(ESignatureMappings.ToDto).ToArray()));
    }
}
