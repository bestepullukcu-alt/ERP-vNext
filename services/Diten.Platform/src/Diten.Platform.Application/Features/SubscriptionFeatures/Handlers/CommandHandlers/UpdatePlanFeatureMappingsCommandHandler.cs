using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class UpdatePlanFeatureMappingsCommandHandler : IRequestHandler<UpdatePlanFeatureMappingsCommand, Response<NoContent>>
{
    private readonly IPlanFeatureMappingRepository _mappingRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IFeatureDefinitionRepository _featureRepository;
    private readonly ILogger<UpdatePlanFeatureMappingsCommandHandler> _logger;

    public UpdatePlanFeatureMappingsCommandHandler(
        IPlanFeatureMappingRepository mappingRepository,
        ISubscriptionPlanRepository planRepository,
        IFeatureDefinitionRepository featureRepository,
        ILogger<UpdatePlanFeatureMappingsCommandHandler> logger)
    {
        _mappingRepository = mappingRepository;
        _planRepository = planRepository;
        _featureRepository = featureRepository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(UpdatePlanFeatureMappingsCommand request, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(request.SubscriptionPlanId, ct);
        if (plan is null)
        {
            return Response<NoContent>.Fail("Subscription plan not found.", 404);
        }

        if (!plan.IsActive)
        {
            return Response<NoContent>.Fail("Inactive subscription plan cannot receive feature mappings.", 400);
        }

        if (request.Request.Mappings.Count == 0)
        {
            return Response<NoContent>.Success(204);
        }

        var duplicates = request.Request.Mappings
            .GroupBy(x => x.FeatureDefinitionId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            return Response<NoContent>.Fail("Duplicate feature mapping in request.", 409);
        }

        foreach (var item in request.Request.Mappings)
        {
            if (!SubscriptionFeatureStatusParser.TryParseAvailabilityStatus(item.AvailabilityStatus, out var availabilityStatus))
            {
                return Response<NoContent>.Fail("AvailabilityStatus must be Included, AddOn, NotAvailable, or Preview.", 400);
            }

            var feature = await _featureRepository.GetByIdAsync(item.FeatureDefinitionId, ct);
            if (feature is null)
            {
                return Response<NoContent>.Fail("Subscription feature not found.", 404);
            }

            if (feature.Status == FeatureDefinitionStatus.Archived && availabilityStatus != PlanFeatureAvailabilityStatus.NotAvailable)
            {
                return Response<NoContent>.Fail("Archived feature cannot be mapped to a plan.", 400);
            }

            var existing = await _mappingRepository.GetByPlanAndFeatureAsync(plan.Id, feature.Id, ct);
            if (existing is not null && item.RowVersion is not { Length: > 0 })
            {
                return Response<NoContent>.Fail("Plan feature mapping changed while you were editing. Reload is required.", 409);
            }

            var mapping = new PlanFeatureMapping
            {
                SubscriptionPlanId = plan.Id,
                FeatureDefinitionId = feature.Id,
                AvailabilityStatus = availabilityStatus,
                EffectiveFromUtc = item.EffectiveFromUtc,
                EffectiveToUtc = item.EffectiveToUtc
            };

            var updated = await _mappingRepository.UpsertAsync(mapping, existing is null ? null : item.RowVersion, ct);
            if (!updated)
            {
                return Response<NoContent>.Fail("Plan feature mapping changed while you were editing. Reload is required.", 409);
            }
        }

        _logger.LogInformation("AUDIT PlanFeatureMappingsUpdated PlanId={PlanId} MappingCount={MappingCount}", plan.Id, request.Request.Mappings.Count);
        return Response<NoContent>.Success(204);
    }
}
