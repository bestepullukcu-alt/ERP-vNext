using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.ESignature;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.CommandHandlers;

public sealed class RecordInternalAttestationHandler
    : IRequestHandler<RecordInternalAttestationCommand, Response<Guid>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IInternalSignaturePolicy _policy;
    private readonly IAuditService _auditService;

    public RecordInternalAttestationHandler(
        IESignatureRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IInternalSignaturePolicy policy,
        IAuditService auditService)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _policy = policy;
        _auditService = auditService;
    }

    public async Task<Response<Guid>> Handle(RecordInternalAttestationCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Response<Guid>.Fail("Authenticated platform actor is required.", 401);
        }

        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, command.TargetTenantId);
        var envelope = await _repository.GetEnvelopeAsync(command.EnvelopeId, ct);
        if (envelope is null)
        {
            return Response<Guid>.Fail("Signature envelope was not found.", 404);
        }

        var participant = await _repository.GetParticipantAsync(command.EnvelopeId, command.Request.ParticipantId, ct);
        if (participant is null)
        {
            return Response<Guid>.Fail("Signature participant was not found.", 404);
        }

        if (participant.SignerUserId != _currentUser.UserId)
        {
            return Response<Guid>.Fail("The current actor cannot attest for another signer.", 403);
        }

        try
        {
            _policy.ValidateAttestation(command.Request.AttestationText);
        }
        catch (InvalidOperationException exception)
        {
            return Response<Guid>.Fail(exception.Message, 400);
        }

        if (envelope.Version != command.Request.ExpectedEnvelopeVersion
            || participant.Version != command.Request.ExpectedParticipantVersion)
        {
            return Response<Guid>.Fail("Signature evidence was modified by another operation.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var expectedEnvelopeVersion = envelope.Version;
        var expectedParticipantVersion = participant.Version;
        var participants = await _repository.GetParticipantsAsync(envelope.Id, ct);
        if (participants.Any(x =>
                x.SigningOrder < participant.SigningOrder
                && x.Status != SignatureParticipantStatus.Signed))
        {
            return Response<Guid>.Fail("Previous signature participants must complete first.", 409);
        }

        participant.MarkSigned(now);

        var attestation = new SignerAttestation
        {
            TenantId = command.TargetTenantId,
            EnvelopeId = envelope.Id,
            ParticipantId = participant.Id,
            SignerUserId = participant.SignerUserId,
            SignerEmailSnapshot = participant.SignerEmail,
            SignerDisplayNameSnapshot = participant.SignerDisplayName,
            AttestationText = command.Request.AttestationText.Trim(),
            PolicyCode = _policy.PolicyCode,
            AttestedAt = now,
            CorrelationId = command.Request.CorrelationId,
            CreatedBy = _currentUser.ActorName
        };

        var allSigned = participants.All(x =>
            x.Id == participant.Id
                ? participant.Status == SignatureParticipantStatus.Signed
                : x.Status == SignatureParticipantStatus.Signed);
        envelope.RecordSignatureProgress(allSigned, now);

        InternalSignatureArtifact? artifact = null;
        SignatureVerificationArtifact? verification = null;
        if (allSigned)
        {
            var content = InternalEvidenceDocumentGenerator.Generate(envelope, participant, attestation);
            var signedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            artifact = new InternalSignatureArtifact
            {
                TenantId = command.TargetTenantId,
                EnvelopeId = envelope.Id,
                ArtifactType = "SignedApprovalPdf",
                FileName = $"{envelope.EnvelopeNumber}.pdf",
                ContentType = "application/pdf",
                Content = content,
                Sha256 = signedHash,
                RetainUntil = now.AddYears(7),
                CreatedBy = _currentUser.ActorName
            };
            verification = new SignatureVerificationArtifact
            {
                TenantId = command.TargetTenantId,
                EnvelopeId = envelope.Id,
                SignedArtifactId = artifact.Id,
                SourceArtifactHash = envelope.SourceArtifactHash,
                SignedArtifactHash = signedHash,
                VerificationStatus = SignatureVerificationStatus.Verified,
                EvidenceReference = $"ESIGN:{envelope.Id:N}:{artifact.Id:N}",
                VerifiedAt = now,
                CorrelationId = command.Request.CorrelationId,
                CreatedBy = _currentUser.ActorName
            };
        }

        var committed = await _repository.CommitAttestationAsync(
            envelope,
            expectedEnvelopeVersion,
            participant,
            expectedParticipantVersion,
            attestation,
            artifact,
            verification,
            ct);
        if (!committed)
        {
            return Response<Guid>.Fail("Signature evidence was modified by another operation.", 409);
        }

        await _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = command.Request.CorrelationId,
            RequestType = "RecordInternalSignatureAttestation",
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = _currentUser.UserId,
            TargetTenantId = envelope.TenantId,
            Category = AuditCategory.Security,
            EntityType = nameof(SignerAttestation),
            EntityId = attestation.Id,
            Operation = AuditOperation.Execute,
            SourceModule = "MOD-0022",
            Metadata = new Dictionary<string, object?>
            {
                ["EnvelopeId"] = envelope.Id,
                ["ParticipantId"] = participant.Id,
                ["PolicyCode"] = attestation.PolicyCode,
                ["Completed"] = allSigned,
                ["VerificationArtifactId"] = verification?.Id
            }
        }, ct);

        return Response<Guid>.Success(attestation.Id);
    }
}
