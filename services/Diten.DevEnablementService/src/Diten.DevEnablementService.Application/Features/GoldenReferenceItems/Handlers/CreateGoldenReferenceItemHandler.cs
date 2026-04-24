using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;
using Diten.DevEnablementService.Domain.Entities;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class CreateGoldenReferenceItemHandler : IRequestHandler<CreateGoldenReferenceItemCommand, Response<Guid>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public CreateGoldenReferenceItemHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateGoldenReferenceItemCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        var existing = await _repository.GetAllAsync(cancellationToken);
        var duplicate = existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return Response<Guid>.Fail("Code must be unique.", 400);
        }

        var entity = new GoldenReferenceItem
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ReferenceType = request.ReferenceType,
            Priority = request.Priority,
            IsActive = true
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        return Response<Guid>.Success(created.Id);
    }
}
