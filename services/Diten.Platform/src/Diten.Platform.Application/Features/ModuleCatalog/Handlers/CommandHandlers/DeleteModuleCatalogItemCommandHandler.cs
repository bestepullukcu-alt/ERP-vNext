using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Diten.Platform.Application.Features.GlobalApplicability;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class DeleteModuleCatalogItemCommandHandler : IRequestHandler<DeleteModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public DeleteModuleCatalogItemCommandHandler(ITransactionalModuleCatalogRepository repository,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(DeleteModuleCatalogItemCommand request, CancellationToken ct)
    {
        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(DeleteModuleCatalogItemCommand), AuditOperation.Delete, "ModuleCatalogItem", request.Id),
            async (session, transactionCt) =>
            {
                var item = await _repository.GetByIdAsync(session, request.Id, transactionCt);
                if (item is null) return new(Response<NoContent>.Fail("Module catalog item not found.", 404), false);
                if (item.IsCoreModule) return new(Response<NoContent>.Fail("Core modules cannot be deleted.", 400), false);
                if (item.Origin == ModuleCatalogOrigin.SelfRegistered) return new(Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409), false);
                await _repository.DeleteAsync(session, item.Id, transactionCt);
                item.IsDeleted = true;
                return new(Response<NoContent>.Success(204), true,
                    (s, version, token) => _state.UpsertModuleCatalogAsync(s, item, version, token));
            }, ct);
    }
}
