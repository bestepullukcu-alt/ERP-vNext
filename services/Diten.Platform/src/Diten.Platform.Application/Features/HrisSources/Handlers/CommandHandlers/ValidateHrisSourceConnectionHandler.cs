using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class ValidateHrisSourceConnectionHandler : IRequestHandler<ValidateHrisSourceConnectionCommand, Response<NoContent>>
{
    private readonly IHrisSourceRepository _repository;

    public ValidateHrisSourceConnectionHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ValidateHrisSourceConnectionCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetSourceProfileByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("HRIS source not found.", 404);
        }

        if (HrisSensitiveValueGuard.LooksLikeRawSecret(entity.ConnectionProfileReference)
            || HrisSensitiveValueGuard.LooksLikeRawPayload(entity.ConnectionProfileReference))
        {
            return Response<NoContent>.Fail("HRIS source connection reference is not safe.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        entity.LastValidatedAt = now;
        await _repository.UpdateSourceProfileAsync(entity, ct);
        await _repository.CreateHealthSnapshotAsync(
            new HrisSourceHealthSnapshot
            {
                TenantId = entity.TenantId,
                SourceProfileId = entity.Id,
                ObservedAt = now,
                HealthState = HrisHealthState.Unknown,
                RedactedMessage = "Provider-neutral contract validated. Provider adapter not configured."
            },
            ct);

        return Response<NoContent>.Success(204);
    }
}
