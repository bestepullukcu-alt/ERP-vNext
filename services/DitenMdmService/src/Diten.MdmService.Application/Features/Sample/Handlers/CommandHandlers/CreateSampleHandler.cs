using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.Sample.Commands;

namespace Diten.MdmService.Application.Features.Sample.Handlers.CommandHandlers;

public sealed class CreateSampleHandler : IRequestHandler<CreateSampleCommand, CreateSampleResult>
{
    private readonly ISampleRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateSampleHandler> _logger;

    public CreateSampleHandler(
        ISampleRepository repository,
        ITenantContext tenantContext,
        ILogger<CreateSampleHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CreateSampleResult> Handle(
        CreateSampleCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SampleEntity
        {
            Name = request.Name,
            Description = request.Description,
            // TenantId sadece server-side TenantContext'ten alınır
            TenantId = _tenantContext.TenantId
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "SampleEntity oluşturuldu. Id={Id} TenantId={TenantId}",
            created.Id,
            created.TenantId);

        return new CreateSampleResult(
            created.Id,
            created.Name,
            created.Description,
            created.TenantId,
            created.CreatedAt);
    }
}
