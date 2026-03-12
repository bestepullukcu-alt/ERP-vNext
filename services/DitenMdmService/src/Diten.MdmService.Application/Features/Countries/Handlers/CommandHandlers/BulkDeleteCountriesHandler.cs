using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Application.Features.Countries.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class BulkDeleteCountriesHandler : IRequestHandler<BulkDeleteCountriesCommand, int>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BulkDeleteCountriesHandler> _logger;

    public BulkDeleteCountriesHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<BulkDeleteCountriesHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<int> Handle(
        BulkDeleteCountriesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deletedCount = 0;

        foreach (var id in request.Ids)
        {
            var entity = await _repository.GetByIdAsync(id, cancellationToken);
            if (entity != null)
            {
                await _repository.DeleteAsync(id, cancellationToken);
                deletedCount++;
            }
        }

        _logger.LogInformation(
            "Bulk delete completed. Count={Count} TenantId={TenantId}",
            deletedCount, _tenantContext.TenantId);

        return deletedCount;
    }
}

