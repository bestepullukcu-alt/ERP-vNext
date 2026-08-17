using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class UpdateHrisSourceProfileHandler : IRequestHandler<UpdateHrisSourceProfileCommand, Response<NoContent>>
{
    private readonly IHrisSourceRepository _repository;

    public UpdateHrisSourceProfileHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdateHrisSourceProfileCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetSourceProfileByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("HRIS source not found.", 404);
        }

        var canonicalCode = HrisSourceCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<NoContent>.Fail("HRIS source code is required.", 400);
        }

        if (await _repository.ExistsActiveSourceCodeAsync(canonicalCode, entity.Id, ct))
        {
            return Response<NoContent>.Fail("HRIS source code already exists.", 409);
        }

        entity.Code = canonicalCode;
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.ProviderKind = request.Request.ProviderKind;
        entity.ExternalTenantKey = NormalizeOptional(request.Request.ExternalTenantKey);
        entity.ConnectionProfileReference = request.Request.ConnectionProfileReference.Trim();
        entity.LifecycleState = request.Request.LifecycleState;
        entity.SyncMode = request.Request.SyncMode;
        entity.MappingProfileId = request.Request.MappingProfileId;
        entity.CorrelationId = NormalizeOptional(request.Request.CorrelationId);

        await _repository.UpdateSourceProfileAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
