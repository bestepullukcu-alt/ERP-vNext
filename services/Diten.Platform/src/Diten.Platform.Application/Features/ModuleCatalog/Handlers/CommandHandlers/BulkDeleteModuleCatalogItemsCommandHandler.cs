using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class BulkDeleteModuleCatalogItemsCommandHandler : IRequestHandler<BulkDeleteModuleCatalogItemsCommand, Response<NoContent>>
{
    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public BulkDeleteModuleCatalogItemsCommandHandler(ITransactionalModuleCatalogRepository repository,
        IGlobalApplicabilityTransactionCoordinator transaction,
        IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(BulkDeleteModuleCatalogItemsCommand request, CancellationToken ct)
    {
        if (request.Ids.Count == 0)
        {
            return Response<NoContent>.Fail("At least one module catalog item id is required.", 400);
        }

        var ids = request.Ids.Distinct().ToArray();
        return await _transaction.ExecuteBatchAsync<Response<NoContent>>(async (session, transactionCt) =>
        {
            var items = new List<Domain.Entities.ModuleCatalogItem>(ids.Length);
            foreach (var id in ids)
            {
                var item = await _repository.GetByIdAsync(session, id, transactionCt);
                if (item is null)
                    return new(Response<NoContent>.Fail("Module catalog item not found.", 404), []);
                if (item.IsCoreModule)
                    return new(Response<NoContent>.Fail("Core modules cannot be deleted.", 400), []);
                items.Add(item);
            }

            var changes = new List<GlobalApplicabilityBatchItem>(items.Count);
            foreach (var item in items)
            {
                await _repository.DeleteAsync(session, item.Id, transactionCt);
                item.IsDeleted = true;
                changes.Add(new(
                    new(nameof(BulkDeleteModuleCatalogItemsCommand), AuditOperation.Delete,
                        "ModuleCatalogItem", item.Id),
                    (s, version, token) => _state.UpsertModuleCatalogAsync(s, item, version, token)));
            }

            return new(Response<NoContent>.Success(204), changes);
        }, ct);
    }
}
