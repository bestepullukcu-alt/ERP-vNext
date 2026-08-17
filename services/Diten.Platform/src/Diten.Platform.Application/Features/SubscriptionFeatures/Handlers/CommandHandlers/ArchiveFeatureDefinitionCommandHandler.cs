using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class ArchiveFeatureDefinitionCommandHandler : IRequestHandler<ArchiveFeatureDefinitionCommand, Response<NoContent>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly ILogger<ArchiveFeatureDefinitionCommandHandler> _logger;

    public ArchiveFeatureDefinitionCommandHandler(
        IFeatureDefinitionRepository repository,
        ILogger<ArchiveFeatureDefinitionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(ArchiveFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _repository.GetByIdAsync(request.Id, ct);
        if (feature is null)
        {
            return Response<NoContent>.Fail("Subscription feature not found.", 404);
        }

        feature.Status = FeatureDefinitionStatus.Archived;
        var updated = await _repository.UpdateAsync(feature, request.RowVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("Subscription feature changed while you were editing. Reload is required.", 409);
        }

        _logger.LogInformation("AUDIT SubscriptionFeatureArchived FeatureId={FeatureId} FeatureCode={FeatureCode}", feature.Id, feature.FeatureCode);
        return Response<NoContent>.Success(204);
    }
}
