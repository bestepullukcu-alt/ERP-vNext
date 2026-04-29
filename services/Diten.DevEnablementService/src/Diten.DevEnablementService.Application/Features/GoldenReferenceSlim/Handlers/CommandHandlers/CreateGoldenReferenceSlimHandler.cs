using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;
using GoldenReferenceSlimEntity = Diten.DevEnablementService.Domain.Entities.GoldenReferenceSlim;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Handlers.CommandHandlers;

public sealed class CreateGoldenReferenceSlimHandler : IRequestHandler<CreateGoldenReferenceSlimCommand, Response<Guid>>
{
    private readonly IGoldenReferenceSlimRepository _repository;

    public CreateGoldenReferenceSlimHandler(IGoldenReferenceSlimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateGoldenReferenceSlimCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        var existing = await _repository.GetAllAsync(cancellationToken);
        var duplicate = existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return Response<Guid>.Fail("Code must be unique.", 400);
        }

        var entity = new GoldenReferenceSlimEntity
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ReferenceType = request.ReferenceType,
            Priority = request.Priority,
            IsActive = request.IsActive
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        return Response<Guid>.Success(created.Id);
    }
}
