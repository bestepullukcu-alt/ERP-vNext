using System.Text;
using Xunit;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Application.Features.ESignature.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.ESignature.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.ESignature.Queries;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums.ESignature;
using Diten.Platform.Domain.Repositories;
using Moq;

namespace Diten.Platform.Application.Tests.ESignature;

public sealed class ESignatureRulesTests
{
    [Fact]
    public void EnvelopeLifecycle_CompletesOnlyFromPendingOrPartiallySigned()
    {
        var envelope = CreateEnvelope();
        envelope.Send(DateTimeOffset.UtcNow);

        envelope.RecordSignatureProgress(false, DateTimeOffset.UtcNow);
        Assert.Equal(SignatureEnvelopeStatus.PartiallySigned, envelope.Status);

        envelope.RecordSignatureProgress(true, DateTimeOffset.UtcNow);
        Assert.Equal(SignatureEnvelopeStatus.Completed, envelope.Status);
        Assert.NotNull(envelope.CompletedAt);
        Assert.Throws<InvalidOperationException>(() =>
            envelope.Cancel(Guid.NewGuid(), "Invalid rollback", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void InternalEvidencePdf_ContainsRequiredNonLegalNotice()
    {
        var envelope = CreateEnvelope();
        envelope.Send(DateTimeOffset.UtcNow);
        var participant = CreateParticipant(envelope.Id);
        var attestation = CreateAttestation(envelope.Id, participant);

        var content = InternalEvidenceDocumentGenerator.Generate(envelope, participant, attestation);
        var text = Encoding.ASCII.GetString(content);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("INTERNAL APPROVAL EVIDENCE", text);
        Assert.Contains("not a provider-verified or qualified electronic signature", text);
        Assert.Contains(envelope.SourceArtifactHash, text);
    }

    [Fact]
    public async Task RecordAttestation_RejectsOptimisticConcurrencyConflict()
    {
        var tenantId = Guid.NewGuid();
        var envelope = CreateEnvelope(tenantId);
        envelope.Send(DateTimeOffset.UtcNow);
        var signerUserId = Guid.NewGuid();
        var participant = CreateParticipant(envelope.Id, tenantId, signerUserId);
        var repository = new Mock<IESignatureRepository>();
        repository.Setup(x => x.GetEnvelopeAsync(envelope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);
        repository.Setup(x => x.GetParticipantAsync(envelope.Id, participant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(participant);

        var tenantContext = new TenantContext();
        var currentUser = CreateCurrentUser(signerUserId);
        var auditService = CreateAuditService();
        var handler = new RecordInternalAttestationHandler(
            repository.Object,
            tenantContext,
            currentUser.Object,
            new InternalSignaturePolicy(),
            auditService.Object);
        var command = new RecordInternalAttestationCommand(
            tenantId,
            envelope.Id,
            new RecordInternalAttestationRequest(
                participant.Id,
                "I approve this internal evidence.",
                envelope.Version + 1,
                participant.Version,
                Guid.NewGuid()));

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        repository.Verify(x => x.CommitAttestationAsync(
            It.IsAny<SignatureEnvelope>(),
            It.IsAny<int>(),
            It.IsAny<SignatureParticipant>(),
            It.IsAny<int>(),
            It.IsAny<SignerAttestation>(),
            It.IsAny<InternalSignatureArtifact?>(),
            It.IsAny<SignatureVerificationArtifact?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task CreateEnvelope_UsesExplicitPlatformTargetTenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IESignatureRepository>();
        repository.Setup(x => x.GenerateEnvelopeNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ESIGN-2026-00000001");
        Guid persistedTenantId = Guid.Empty;
        repository.Setup(x => x.CreateEnvelopeAsync(
                It.IsAny<SignatureEnvelope>(),
                It.IsAny<IReadOnlyList<SignatureParticipant>>(),
                It.IsAny<CancellationToken>()))
            .Callback<SignatureEnvelope, IReadOnlyList<SignatureParticipant>, CancellationToken>(
                (envelope, participants, _) =>
                {
                    persistedTenantId = envelope.TenantId;
                    Assert.All(participants, participant => Assert.Equal(tenantId, participant.TenantId));
                })
            .Returns(Task.CompletedTask);

        var tenantContext = new TenantContext();
        var sourceContent = Encoding.UTF8.GetBytes("immutable controlled document v1");
        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceContent))
            .ToLowerInvariant();
        var handler = new CreateSignatureEnvelopeHandler(
            repository.Object,
            tenantContext,
            CreateCurrentUser().Object,
            CreateAuditService().Object);
        var response = await handler.Handle(
            new CreateSignatureEnvelopeCommand(
                tenantId,
                new CreateSignatureEnvelopeRequest(
                    "ControlledDocument",
                    Guid.NewGuid(),
                    "v1",
                    Guid.NewGuid(),
                    sourceHash,
                    sourceContent,
                    Guid.NewGuid(),
                    [new CreateSignatureParticipantRequest(
                        Guid.NewGuid(),
                        "signer@example.com",
                        "Signer",
                        1,
                        "Approver")])),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(tenantId, persistedTenantId);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task CreateEnvelope_RejectsSourceArtifactHashMismatch()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IESignatureRepository>();
        var handler = new CreateSignatureEnvelopeHandler(
            repository.Object,
            new TenantContext(),
            CreateCurrentUser().Object,
            CreateAuditService().Object);

        var response = await handler.Handle(
            new CreateSignatureEnvelopeCommand(
                tenantId,
                new CreateSignatureEnvelopeRequest(
                    "ControlledDocument",
                    Guid.NewGuid(),
                    "v1",
                    Guid.NewGuid(),
                    new string('a', 64),
                    Encoding.UTF8.GetBytes("different content"),
                    Guid.NewGuid(),
                    [new CreateSignatureParticipantRequest(
                        Guid.NewGuid(),
                        "signer@example.com",
                        "Signer",
                        1,
                        "Approver")])),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        repository.Verify(x => x.CreateEnvelopeAsync(
            It.IsAny<SignatureEnvelope>(),
            It.IsAny<IReadOnlyList<SignatureParticipant>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordAttestation_RejectsActorSigningForAnotherParticipant()
    {
        var tenantId = Guid.NewGuid();
        var envelope = CreateEnvelope(tenantId);
        envelope.Send(DateTimeOffset.UtcNow);
        var participant = CreateParticipant(envelope.Id, tenantId, Guid.NewGuid());
        var repository = new Mock<IESignatureRepository>();
        repository.Setup(x => x.GetEnvelopeAsync(envelope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);
        repository.Setup(x => x.GetParticipantAsync(envelope.Id, participant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(participant);
        var handler = new RecordInternalAttestationHandler(
            repository.Object,
            new TenantContext(),
            CreateCurrentUser(Guid.NewGuid()).Object,
            new InternalSignaturePolicy(),
            CreateAuditService().Object);

        var response = await handler.Handle(
            new RecordInternalAttestationCommand(
                tenantId,
                envelope.Id,
                new RecordInternalAttestationRequest(
                    participant.Id,
                    "I approve this internal evidence.",
                    envelope.Version,
                    participant.Version,
                    Guid.NewGuid())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        repository.Verify(x => x.CommitAttestationAsync(
            It.IsAny<SignatureEnvelope>(),
            It.IsAny<int>(),
            It.IsAny<SignatureParticipant>(),
            It.IsAny<int>(),
            It.IsAny<SignerAttestation>(),
            It.IsAny<InternalSignatureArtifact?>(),
            It.IsAny<SignatureVerificationArtifact?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetArtifact_RejectsStoredContentHashMismatch()
    {
        var tenantId = Guid.NewGuid();
        var artifact = new InternalSignatureArtifact
        {
            TenantId = tenantId,
            EnvelopeId = Guid.NewGuid(),
            ArtifactType = "SignedApprovalPdf",
            FileName = "evidence.pdf",
            ContentType = "application/pdf",
            Content = Encoding.UTF8.GetBytes("tampered"),
            Sha256 = new string('a', 64),
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
            CreatedBy = "tester"
        };
        var repository = new Mock<IESignatureRepository>();
        repository.Setup(x => x.GetArtifactAsync(artifact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artifact);
        var handler = new GetSignatureArtifactHandler(repository.Object, new TenantContext());

        var response = await handler.Handle(
            new GetSignatureArtifactQuery(tenantId, artifact.Id),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    private static SignatureEnvelope CreateEnvelope(Guid? tenantId = null) =>
        new()
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            EnvelopeNumber = "ESIGN-2026-00000001",
            SubjectType = "ControlledDocument",
            SubjectId = Guid.NewGuid(),
            SubjectVersion = "v1",
            DocumentArtifactId = Guid.NewGuid(),
            SourceArtifactHash = new string('a', 64),
            RequestedBy = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CreatedBy = "tester"
        };

    private static SignatureParticipant CreateParticipant(
        Guid envelopeId,
        Guid? tenantId = null,
        Guid? signerUserId = null) =>
        new()
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            EnvelopeId = envelopeId,
            SignerUserId = signerUserId ?? Guid.NewGuid(),
            SignerEmail = "signer@example.com",
            SignerDisplayName = "Signer",
            SigningOrder = 1,
            Role = "Approver",
            CreatedBy = "tester"
        };

    private static SignerAttestation CreateAttestation(Guid envelopeId, SignatureParticipant participant) =>
        new()
        {
            TenantId = participant.TenantId,
            EnvelopeId = envelopeId,
            ParticipantId = participant.Id,
            SignerUserId = participant.SignerUserId,
            SignerEmailSnapshot = participant.SignerEmail,
            SignerDisplayNameSnapshot = participant.SignerDisplayName,
            AttestationText = "I approve this internal evidence.",
            AttestedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CreatedBy = "tester"
        };

    private static Mock<ICurrentUserContext> CreateCurrentUser(Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.UserId).Returns(userId ?? Guid.NewGuid());
        currentUser.SetupGet(x => x.ActorName).Returns("platform-admin@example.com");
        currentUser.SetupGet(x => x.Email).Returns("platform-admin@example.com");
        currentUser.SetupGet(x => x.DisplayName).Returns("Platform Admin");
        return currentUser;
    }

    private static Mock<IAuditService> CreateAuditService()
    {
        var auditService = new Mock<IAuditService>();
        auditService.Setup(x => x.AppendAsync(
                It.IsAny<AuditAppendRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuditAppendResult.Queued("audit:test"));
        return auditService;
    }
}
