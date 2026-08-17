using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class CreateFeatureDefinitionCommandHandler : IRequestHandler<CreateFeatureDefinitionCommand, Response<Guid>>
{
    private readonly IFeatureDefinitionRepository _repository;
    private readonly IFeatureCategoryRepository _categoryRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<CreateFeatureDefinitionCommandHandler> _logger;

    public CreateFeatureDefinitionCommandHandler(
        IFeatureDefinitionRepository repository,
        IFeatureCategoryRepository categoryRepository,
        ICurrentUserContext currentUser,
        ILogger<CreateFeatureDefinitionCommandHandler> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var normalizedCode = SubscriptionFeatureCodeNormalizer.Normalize(request.Request.FeatureCode);
        if (await _repository.ExistsByCodeAsync(normalizedCode, ct: ct))
        {
            return Response<Guid>.Fail("FeatureCode already exists.", 409);
        }

        var normalizedSlug = SubscriptionFeatureSlugNormalizer.Normalize(request.Request.FeatureSlug);
        if (await _repository.ExistsBySlugAsync(normalizedSlug, ct: ct))
        {
            return Response<Guid>.Fail("FeatureSlug already exists.", 409);
        }

        SubscriptionFeatureStatusParser.TryParseFeatureStatus(request.Request.Status, out var status);
        var categoryValidation = await ValidateCategoryAsync(request.Request.CategoryId, status, ct);
        if (!categoryValidation.IsValid)
        {
            return Response<Guid>.Fail(categoryValidation.Error!, categoryValidation.StatusCode);
        }

        var feature = new FeatureDefinition
        {
            FeatureCode = normalizedCode,
            FeatureSlug = normalizedSlug,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            CategoryId = request.Request.CategoryId,
            Status = status,
            IsCoreFeature = request.Request.IsCoreFeature,
            SortOrder = request.Request.SortOrder ?? 0,
            OptionalFeatureFlagKey = string.IsNullOrWhiteSpace(request.Request.OptionalFeatureFlagKey) ? null : request.Request.OptionalFeatureFlagKey.Trim(),
            CreatedByUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null
        };

        await _repository.CreateAsync(feature, ct);

        _logger.LogInformation("AUDIT SubscriptionFeatureCreated FeatureId={FeatureId} FeatureCode={FeatureCode}", feature.Id, feature.FeatureCode);
        return Response<Guid>.Success(feature.Id, 201);
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
