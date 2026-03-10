using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class DeleteCountryHandler : IRequestHandler<DeleteCountryCommand, bool>
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

    public async Task<bool> Handle(
        DeleteCountryCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsAsync(request.Id, cancellationToken);
        if (!exists)
            return false;

        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}