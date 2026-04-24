using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class UpdateGoldenReferenceItemHandler : IRequestHandler<UpdateGoldenReferenceItemCommand, Response<bool>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public UpdateGoldenReferenceItemHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateGoldenReferenceItemCommand request, CancellationToken cancellationToken)
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
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(updated);
    }
}
