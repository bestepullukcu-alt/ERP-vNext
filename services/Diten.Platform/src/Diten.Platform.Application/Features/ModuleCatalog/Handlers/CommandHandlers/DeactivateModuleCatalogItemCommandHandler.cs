using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Diten.Platform.Application.Features.GlobalApplicability;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class DeactivateModuleCatalogItemCommandHandler : IRequestHandler<DeactivateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public DeactivateModuleCatalogItemCommandHandler(ITransactionalModuleCatalogRepository repository,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(DeactivateModuleCatalogItemCommand request, CancellationToken ct)
    {
        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(DeactivateModuleCatalogItemCommand), AuditOperation.Deactivate, "ModuleCatalogItem", request.Id),
            async (session, transactionCt) =>
            {
                var item = await _repository.GetByIdAsync(session, request.Id, transactionCt);
                if (item is null) return new(Response<NoContent>.Fail("Module catalog item not found.", 404), false);
                if (item.IsBaseline) return new(Response<NoContent>.Fail(ModuleCatalogErrorCodes.BaselineCannotBeDeactivated, 409), false);
                if (item.Status != ModuleCatalogStatus.Active) return new(Response<NoContent>.Fail($"Invalid status transition from {item.Status} to Inactive.", 400), false);
                item.Status = ModuleCatalogStatus.Inactive;
                await _repository.UpdateAsync(session, item, transactionCt);
                return new(Response<NoContent>.Success(204), true,
                    (s, version, token) => _state.UpsertModuleCatalogAsync(s, item, version, token));
            }, ct);
    }
}
