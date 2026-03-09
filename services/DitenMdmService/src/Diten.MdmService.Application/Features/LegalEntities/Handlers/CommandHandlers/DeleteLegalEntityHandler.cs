using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Commands;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.CommandHandlers;

public sealed class DeleteLegalEntityHandler : IRequestHandler<DeleteLegalEntityCommand, bool>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteLegalEntityHandler> _logger;

    public DeleteLegalEntityHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<DeleteLegalEntityHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<bool> Handle(
        DeleteLegalEntityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exists = await _repository.ExistsAsync(request.Id, cancellationToken);
        if (!exists)
        {
            _logger.LogWarning("LegalEntity not found for deletion. Id={Id} TenantId={TenantId}", request.Id, _tenantContext.TenantId);
            throw new KeyNotFoundException("LegalEntity.Error.NotFound");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);

        _logger.LogInformation("LegalEntity deleted. Id={Id} TenantId={TenantId}", request.Id, _tenantContext.TenantId);

        return true;
    }
}
