using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;
using GoldenReferenceCompactEntity = Diten.DevEnablementService.Domain.Entities.GoldenReferenceCompact;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.CommandHandlers;

public sealed class CreateGoldenReferenceCompactHandler : IRequestHandler<CreateGoldenReferenceCompactCommand, Response<Guid>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public CreateGoldenReferenceCompactHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateGoldenReferenceCompactCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        var existing = await _repository.GetAllAsync(cancellationToken);
        var duplicate = existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return Response<Guid>.Fail("Code must be unique.", 400);
        }

        var entity = new GoldenReferenceCompactEntity
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ReferenceType = request.ReferenceType,
            Category = request.Category,
            GroupKey = request.GroupKey,
            SourceSystem = request.SourceSystem,
            Owner = request.Owner,
            Version = request.Version,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            Priority = request.Priority,
            IsActive = request.IsActive
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        return Response<Guid>.Success(created.Id);
    }
}
