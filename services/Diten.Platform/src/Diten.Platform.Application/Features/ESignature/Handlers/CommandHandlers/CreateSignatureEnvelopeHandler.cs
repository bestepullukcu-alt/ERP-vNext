using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.CommandHandlers;

public sealed class CreateSignatureEnvelopeHandler
    : IRequestHandler<CreateSignatureEnvelopeCommand, Response<Guid>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditService _auditService;

    public CreateSignatureEnvelopeHandler(
        IESignatureRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Response<Guid>> Handle(CreateSignatureEnvelopeCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Response<Guid>.Fail("Authenticated platform actor is required.", 401);
        }

        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, command.TargetTenantId);
        var request = command.Request;
        var computedSourceHash = Convert.ToHexString(SHA256.HashData(request.SourceArtifactContent))
            .ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(computedSourceHash),
                Convert.FromHexString(request.SourceArtifactHash)))
        {
            return Response<Guid>.Fail("Source artifact content does not match SourceArtifactHash.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var envelope = new SignatureEnvelope
        {
            TenantId = command.TargetTenantId,
            EnvelopeNumber = await _repository.GenerateEnvelopeNumberAsync(ct),
            SubjectType = request.SubjectType.Trim(),
            SubjectId = request.SubjectId,
            SubjectVersion = request.SubjectVersion.Trim(),
            DocumentArtifactId = request.DocumentArtifactId,
            SourceArtifactHash = request.SourceArtifactHash,
            RequestedBy = _currentUser.UserId,
            CorrelationId = request.CorrelationId,
            CreatedBy = _currentUser.ActorName
        };
        envelope.Send(now);

        var participants = request.Participants
            .OrderBy(x => x.SigningOrder)
            .Select(x => new SignatureParticipant
            {
                TenantId = command.TargetTenantId,
                EnvelopeId = envelope.Id,
                SignerUserId = x.SignerUserId,
                SignerEmail = x.SignerEmail.Trim().ToLowerInvariant(),
                SignerDisplayName = x.SignerDisplayName.Trim(),
                SigningOrder = x.SigningOrder,
                Role = x.Role.Trim(),
                CreatedBy = _currentUser.ActorName
            })
            .ToArray();

        await _repository.CreateEnvelopeAsync(envelope, participants, ct);
        await AppendAuditAsync(envelope, AuditOperation.Create, "ESignatureEnvelopeCreated", ct);
        return Response<Guid>.Success(envelope.Id, 201);
    }

    private Task<AuditAppendResult> AppendAuditAsync(
        SignatureEnvelope envelope,
        AuditOperation operation,
        string requestType,
        CancellationToken ct) =>
        _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = envelope.CorrelationId,
            RequestType = requestType,
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = _currentUser.UserId,
            TargetTenantId = envelope.TenantId,
            Category = AuditCategory.Security,
            EntityType = nameof(SignatureEnvelope),
            EntityId = envelope.Id,
            Operation = operation,
            SourceModule = "MOD-0022",
            Metadata = new Dictionary<string, object?>
            {
                ["EnvelopeNumber"] = envelope.EnvelopeNumber,
                ["PolicyCode"] = envelope.PolicyCode,
                ["EvidenceMode"] = "InternalSubstitute"
            }
        }, ct);
}
