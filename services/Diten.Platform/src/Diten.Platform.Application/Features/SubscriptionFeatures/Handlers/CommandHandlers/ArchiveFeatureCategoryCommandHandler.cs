using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;

public sealed class ArchiveFeatureCategoryCommandHandler : IRequestHandler<ArchiveFeatureCategoryCommand, Response<NoContent>>
{
    private readonly IFeatureCategoryRepository _repository;
    private readonly ILogger<ArchiveFeatureCategoryCommandHandler> _logger;

    public ArchiveFeatureCategoryCommandHandler(IFeatureCategoryRepository repository, ILogger<ArchiveFeatureCategoryCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(ArchiveFeatureCategoryCommand request, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(request.Id, ct);
        if (category is null)
        {
            return Response<NoContent>.Fail("Feature category not found.", 404);
        }

        category.Status = FeatureCategoryStatus.Archived;
        var updated = await _repository.UpdateAsync(category, request.RowVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("Feature category changed while you were editing. Reload is required.", 409);
        }

        _logger.LogInformation("AUDIT FeatureCategoryArchived CategoryId={CategoryId} CategoryCode={CategoryCode}", category.Id, category.CategoryCode);
        return Response<NoContent>.Success(204);
    }
}
