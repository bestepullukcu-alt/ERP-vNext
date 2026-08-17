using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class CreateFeatureCategoryCommandHandler : IRequestHandler<CreateFeatureCategoryCommand, Response<Guid>>
{
    private readonly IFeatureCategoryRepository _repository;
    private readonly ILogger<CreateFeatureCategoryCommandHandler> _logger;

    public CreateFeatureCategoryCommandHandler(IFeatureCategoryRepository repository, ILogger<CreateFeatureCategoryCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateFeatureCategoryCommand request, CancellationToken ct)
    {
        var normalizedCode = SubscriptionFeatureCodeNormalizer.Normalize(request.Request.CategoryCode);
        if (await _repository.ExistsByCodeAsync(normalizedCode, ct: ct))
        {
            return Response<Guid>.Fail("CategoryCode already exists.", 409);
        }

        SubscriptionFeatureStatusParser.TryParseCategoryStatus(request.Request.Status, out var status);

        var category = new FeatureCategory
        {
            CategoryCode = normalizedCode,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            SortOrder = request.Request.SortOrder ?? 0,
            Status = status
        };

        await _repository.CreateAsync(category, ct);

        _logger.LogInformation("AUDIT FeatureCategoryCreated CategoryId={CategoryId} CategoryCode={CategoryCode}", category.Id, category.CategoryCode);
        return Response<Guid>.Success(category.Id, 201);
    }
}
