using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class UpdateFeatureDefinitionCommandHandler : IRequestHandler<UpdateFeatureDefinitionCommand, Response<NoContent>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly IFeatureCategoryRepository _categoryRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<UpdateFeatureDefinitionCommandHandler> _logger;

    public UpdateFeatureDefinitionCommandHandler(
        IFeatureDefinitionRepository repository,
        IFeatureCategoryRepository categoryRepository,
        ICurrentUserContext currentUser,
        ILogger<UpdateFeatureDefinitionCommandHandler> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(UpdateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _repository.GetByIdAsync(request.Id, ct);
        if (feature is null)
        {
            return Response<NoContent>.Fail("Subscription feature not found.", 404);
        }

        var normalizedCode = SubscriptionFeatureCodeNormalizer.Normalize(request.Request.FeatureCode);
        if (await _repository.ExistsByCodeAsync(normalizedCode, excludeId: feature.Id, ct: ct))
        {
            return Response<NoContent>.Fail("FeatureCode already exists.", 409);
        }

        var normalizedSlug = SubscriptionFeatureSlugNormalizer.Normalize(request.Request.FeatureSlug);
        if (await _repository.ExistsBySlugAsync(normalizedSlug, excludeId: feature.Id, ct: ct))
        {
            return Response<NoContent>.Fail("FeatureSlug already exists.", 409);
        }

        SubscriptionFeatureStatusParser.TryParseFeatureStatus(request.Request.Status, out var status);
        var categoryValidation = await ValidateCategoryAsync(request.Request.CategoryId, status, ct);
        if (!categoryValidation.IsValid)
        {
            return Response<NoContent>.Fail(categoryValidation.Error!, categoryValidation.StatusCode);
        }

        feature.FeatureCode = normalizedCode;
        feature.FeatureSlug = normalizedSlug;
        feature.DisplayName = request.Request.DisplayName.Trim();
        feature.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        feature.CategoryId = request.Request.CategoryId;
        feature.Status = status;
        feature.IsCoreFeature = request.Request.IsCoreFeature;
        feature.SortOrder = request.Request.SortOrder ?? 0;
        feature.OptionalFeatureFlagKey = string.IsNullOrWhiteSpace(request.Request.OptionalFeatureFlagKey) ? null : request.Request.OptionalFeatureFlagKey.Trim();
        feature.UpdatedByUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;

        var updated = await _repository.UpdateAsync(feature, request.Request.RowVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("Subscription feature changed while you were editing. Reload is required.", 409);
        }

        _logger.LogInformation("AUDIT SubscriptionFeatureUpdated FeatureId={FeatureId} FeatureCode={FeatureCode}", feature.Id, feature.FeatureCode);
        return Response<NoContent>.Success(204);
    }

    private async Task<(bool IsValid, string? Error, int StatusCode)> ValidateCategoryAsync(
        Guid? categoryId,
        FeatureDefinitionStatus status,
        CancellationToken ct)
    {
        if (!categoryId.HasValue)
        {
            return status == FeatureDefinitionStatus.Active
                ? (false, "CategoryId is required when Status is Active.", 400)
                : (true, null, 0);
        }

        var category = await _categoryRepository.GetByIdAsync(categoryId.Value, ct);
        if (category is null)
        {
            return (false, "Feature category not found.", 404);
        }

        if (category.Status == FeatureCategoryStatus.Archived)
        {
            return (false, "Archived category cannot be assigned to a feature.", 400);
        }

        return (true, null, 0);
    }
}
