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

public sealed class CancelSignatureEnvelopeHandler
    : IRequestHandler<CancelSignatureEnvelopeCommand, Response<NoContent>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditService _auditService;

    public CancelSignatureEnvelopeHandler(
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

    public async Task<Response<NoContent>> Handle(CancelSignatureEnvelopeCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Response<NoContent>.Fail("Authenticated platform actor is required.", 401);
        }

        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, command.TargetTenantId);
        var envelope = await _repository.GetEnvelopeAsync(command.EnvelopeId, ct);
        if (envelope is null)
        {
            return Response<NoContent>.Fail("Signature envelope was not found.", 404);
        }

        if (envelope.Version != command.Request.ExpectedVersion)
        {
            return Response<NoContent>.Fail("Signature envelope was modified by another operation.", 409);
        }

        var expectedVersion = envelope.Version;
        try
        {
            envelope.Cancel(_currentUser.UserId, command.Request.Reason, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Response<NoContent>.Fail(exception.Message, 409);
        }

        if (!await _repository.UpdateEnvelopeAsync(envelope, expectedVersion, ct))
        {
            return Response<NoContent>.Fail("Signature envelope was modified by another operation.", 409);
        }

        await _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = command.Request.CorrelationId,
            RequestType = "CancelInternalSignatureEnvelope",
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = _currentUser.UserId,
            TargetTenantId = envelope.TenantId,
            Category = AuditCategory.Security,
            EntityType = nameof(SignatureEnvelope),
            EntityId = envelope.Id,
            Operation = AuditOperation.Update,
            SourceModule = "MOD-0022",
            Metadata = new Dictionary<string, object?>
            {
                ["EnvelopeNumber"] = envelope.EnvelopeNumber,
                ["Reason"] = envelope.CancelReason
            }
        }, ct);

        return Response<NoContent>.Success(204);
    }
}
