using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Commands;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.CommandHandlers;

public sealed class BulkDeleteLegalEntitiesHandler : IRequestHandler<BulkDeleteLegalEntitiesCommand, int>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BulkDeleteLegalEntitiesHandler> _logger;

    public BulkDeleteLegalEntitiesHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<BulkDeleteLegalEntitiesHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<int> Handle(
        BulkDeleteLegalEntitiesCommand request,
        CancellationToken cancellationToken)
    {
        var deletedCount = 0;

        foreach (var id in request.Ids)
        {
            await _repository.DeleteAsync(id, _tenantContext.TenantId, cancellationToken);
            deletedCount++;
        }

        _logger.LogInformation(
            "Bulk delete completed. Count={Count} TenantId={TenantId}",
            deletedCount, _tenantContext.TenantId);

        return deletedCount;
    }
}
