using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.CommandHandlers;

public sealed class GenerateSignatureAuditExportHandler
    : IRequestHandler<GenerateSignatureAuditExportCommand, Response<Guid>>
{
    private readonly IESignatureRepository _repository;
    private readonly IInternalSignatureArtifactStore _artifactStore;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditService _auditService;

    public GenerateSignatureAuditExportHandler(
        IESignatureRepository repository,
        IInternalSignatureArtifactStore artifactStore,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _artifactStore = artifactStore;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Response<Guid>> Handle(GenerateSignatureAuditExportCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Response<Guid>.Fail("Authenticated platform actor is required.", 401);
        }

        if (command.CorrelationId == Guid.Empty)
        {
            return Response<Guid>.Fail("CorrelationId is required.", 400);
        }

        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, command.TargetTenantId);
        var envelope = await _repository.GetEnvelopeAsync(command.EnvelopeId, ct);
        if (envelope is null)
        {
            return Response<Guid>.Fail("Signature envelope was not found.", 404);
        }

        var participants = await _repository.GetParticipantsAsync(envelope.Id, ct);
        var attestations = await _repository.GetAttestationsAsync(envelope.Id, ct);
        var verifications = await _repository.GetVerificationsAsync(envelope.Id, ct);
        var content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            evidenceType = "Internal Approval Evidence",
            legalNotice = "Not a provider-verified or qualified electronic signature.",
            envelope,
            participants,
            attestations,
            verifications,
            exportedAtUtc = DateTimeOffset.UtcNow,
            command.CorrelationId
        }, new JsonSerializerOptions { WriteIndented = true });

        var stored = await _artifactStore.StoreAsync(
            envelope.Id,
            "AuditExport",
            $"{envelope.EnvelopeNumber}-audit.json",
            "application/json",
            content,
            ct);
        var now = DateTimeOffset.UtcNow;
        await _repository.AddAuditExportAsync(new SignatureAuditExport
        {
            TenantId = command.TargetTenantId,
            EnvelopeId = envelope.Id,
            ArtifactId = stored.Id,
            RequestedBy = _currentUser.UserId,
            RequestedAt = now,
            CompletedAt = now,
            CorrelationId = command.CorrelationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        await _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = command.CorrelationId,
            RequestType = "GenerateSignatureAuditExport",
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = _currentUser.UserId,
            TargetTenantId = envelope.TenantId,
            Category = AuditCategory.DataExport,
            EntityType = nameof(SignatureEnvelope),
            EntityId = envelope.Id,
            Operation = AuditOperation.Export,
            SourceModule = "MOD-0022",
            Metadata = new Dictionary<string, object?>
            {
                ["ArtifactId"] = stored.Id,
                ["ArtifactSha256"] = stored.Sha256,
                ["EvidenceMode"] = "InternalSubstitute"
            }
        }, ct);

        return Response<Guid>.Success(stored.Id, 201);
    }
}
