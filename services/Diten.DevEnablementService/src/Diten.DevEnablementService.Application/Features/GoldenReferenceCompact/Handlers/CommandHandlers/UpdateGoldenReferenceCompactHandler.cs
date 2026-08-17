using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.CommandHandlers;

public sealed class UpdateGoldenReferenceCompactHandler : IRequestHandler<UpdateGoldenReferenceCompactCommand, Response<bool>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public UpdateGoldenReferenceCompactHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateGoldenReferenceCompactCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Record not found.", 404);
        }

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        var existing = await _repository.GetAllAsync(cancellationToken);
        var duplicate = existing.Any(x => x.Id != request.Id && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return Response<bool>.Fail("Code must be unique.", 400);
        }

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.ReferenceType = request.ReferenceType;
        entity.Category = request.Category;
        entity.GroupKey = request.GroupKey;
        entity.SourceSystem = request.SourceSystem;
        entity.Owner = request.Owner;
        entity.Version = request.Version;
        entity.EffectiveDate = request.EffectiveDate;
        entity.ExpirationDate = request.ExpirationDate;
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(updated);
    }
}
