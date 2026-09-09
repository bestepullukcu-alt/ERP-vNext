using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class CreateLegalEntityHandler : IRequestHandler<Commands.CreateLegalEntityCommand, Response<Guid>>
{
    private readonly ILegalEntityRepository _repository;

    public CreateLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(Commands.CreateLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim();
        if (await _repository.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            return Response<Guid>.Fail("A Legal Entity with this code already exists.", 409);
        }

        // Parent validation: must exist within the same tenant. GetByIdAsync is tenant-scoped, so a
        // parent from another tenant simply resolves to null here and is rejected (no cross-tenant parenting).
        if (request.ParentId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
            {
                return Response<Guid>.Fail("Parent Legal Entity was not found in this tenant.", 400);
            }
        }

        var entity = new Domain.Entities.LegalEntity
        {
            Code = normalizedCode,
            LegalName = request.LegalName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            ParentId = request.ParentId
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }
}
