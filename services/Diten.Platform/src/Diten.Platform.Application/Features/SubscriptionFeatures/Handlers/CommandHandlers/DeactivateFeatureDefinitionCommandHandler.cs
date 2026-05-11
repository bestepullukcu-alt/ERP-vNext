using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class DeactivateFeatureDefinitionCommandHandler : IRequestHandler<DeactivateFeatureDefinitionCommand, Response<NoContent>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly ILogger<DeactivateFeatureDefinitionCommandHandler> _logger;

    public DeactivateFeatureDefinitionCommandHandler(
        IFeatureDefinitionRepository repository,
        ILogger<DeactivateFeatureDefinitionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeactivateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _repository.GetByIdAsync(request.Id, ct);
        if (feature is null)
        {
            return Response<NoContent>.Fail("Subscription feature not found.", 404);
        }

        if (feature.Status == FeatureDefinitionStatus.Archived)
        {
            return Response<NoContent>.Fail("Archived feature cannot be deactivated.", 400);
        }

        feature.Status = FeatureDefinitionStatus.Inactive;
        var updated = await _repository.UpdateAsync(feature, request.RowVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("Subscription feature changed while you were editing. Reload is required.", 409);
        }

        _logger.LogInformation("AUDIT SubscriptionFeatureDeactivated FeatureId={FeatureId} FeatureCode={FeatureCode}", feature.Id, feature.FeatureCode);
        return Response<NoContent>.Success(204);
    }
}
