using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class DeleteCountryHandler : IRequestHandler<DeleteCountryCommand, Unit>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteCountryHandler> _logger;

    public DeleteCountryHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<DeleteCountryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        DeleteCountryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _repository.DeleteAsync(request.Id, cancellationToken);

        _logger.LogInformation(
            "Country deleted (soft). Id={Id} TenantId={TenantId}",
            request.Id, _tenantContext.TenantId);

        return Unit.Value;
    }
}

