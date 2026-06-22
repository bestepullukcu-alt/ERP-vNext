using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class UpdateFeatureCategoryCommandHandler : IRequestHandler<UpdateFeatureCategoryCommand, Response<NoContent>>
{
    private readonly IFeatureCategoryRepository _repository;
    private readonly ILogger<UpdateFeatureCategoryCommandHandler> _logger;

    public UpdateFeatureCategoryCommandHandler(IFeatureCategoryRepository repository, ILogger<UpdateFeatureCategoryCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(UpdateFeatureCategoryCommand request, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(request.Id, ct);
        if (category is null)
        {
            return Response<NoContent>.Fail("Feature category not found.", 404);
        }

        SubscriptionFeatureStatusParser.TryParseCategoryStatus(request.Request.Status, out var status);

        // CategoryCode is immutable and intentionally not updated here.
        category.DisplayName = request.Request.DisplayName.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        category.SortOrder = request.Request.SortOrder ?? 0;
        category.Status = status;

        var updated = await _repository.UpdateAsync(category, request.Request.RowVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("Feature category changed while you were editing. Reload is required.", 409);
        }

        _logger.LogInformation("AUDIT FeatureCategoryUpdated CategoryId={CategoryId} CategoryCode={CategoryCode}", category.Id, category.CategoryCode);
        return Response<NoContent>.Success(204);
    }
}
