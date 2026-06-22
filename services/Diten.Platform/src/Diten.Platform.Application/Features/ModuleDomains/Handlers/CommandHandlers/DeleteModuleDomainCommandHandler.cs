using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleDomains.Handlers.CommandHandlers;

public sealed class DeleteModuleDomainCommandHandler : IRequestHandler<DeleteModuleDomainCommand, Response<NoContent>>
{
    private readonly IModuleDomainRepository _repository;

    public DeleteModuleDomainCommandHandler(IModuleDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModuleDomainCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module domain not found.", 404);
        }

        await _repository.DeleteAsync(item.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
