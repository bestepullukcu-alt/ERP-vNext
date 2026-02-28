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
        await _repository.DeleteAsync(request.Id, _tenantContext.TenantId, cancellationToken);

        _logger.LogInformation("LegalEntity deleted. Id={Id} TenantId={TenantId}", request.Id, _tenantContext.TenantId);

        return true;
    }
}
