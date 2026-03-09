using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Commands;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.CommandHandlers;

public sealed class UpdateLegalEntityHandler : IRequestHandler<UpdateLegalEntityCommand, bool>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateLegalEntityHandler> _logger;

    public UpdateLegalEntityHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<UpdateLegalEntityHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<bool> Handle(
        UpdateLegalEntityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("LegalEntity.Validation.TitleRequired", nameof(request.Title));
        }

        if (request.ParentLegalEntityId.HasValue)
        {
            if (request.ParentLegalEntityId.Value == request.Id)
            {
                _logger.LogWarning("Circular reference detected during update. Id={Id} ParentId={ParentId}", request.Id, request.ParentLegalEntityId);
                throw new ArgumentException("LegalEntity.Error.CircularReference");
            }

            var exists = await _repository.ExistsAsync(request.ParentLegalEntityId.Value, cancellationToken);
            if (!exists)
            {
                _logger.LogWarning("Parent LegalEntity not found during update. ParentId={ParentId} TenantId={TenantId}", request.ParentLegalEntityId, _tenantContext.TenantId);
                throw new KeyNotFoundException("LegalEntity.Error.ParentNotFound");
            }
        }

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("LegalEntity not found. Id={Id} TenantId={TenantId}", request.Id, _tenantContext.TenantId);
            throw new KeyNotFoundException("LegalEntity.Error.NotFound");
        }

        entity.Title = request.Title;
        entity.TaxOffice = request.TaxOffice;
        entity.TaxNumber = request.TaxNumber;
        entity.Email = request.Email;
        entity.Phone = request.Phone;
        entity.Website = request.Website;
        entity.Address = request.Address;
        entity.CompanyType = request.CompanyType;
        entity.Sector = request.Sector;
        entity.ContactPerson = request.ContactPerson;
        entity.PrimaryCurrency = request.PrimaryCurrency;
        entity.DefaultTimeZone = request.DefaultTimeZone;
        entity.ParentLegalEntityId = request.ParentLegalEntityId;
        entity.DefaultCommunicationLanguage = request.DefaultCommunicationLanguage;
        entity.OrganizationRole = request.OrganizationRole;
        entity.LogoUrl = request.LogoUrl;
        entity.FiscalYearStart = request.FiscalYearStart;
        entity.RegistrationDate = request.RegistrationDate;
        entity.EffectiveFromDate = request.EffectiveFromDate;
        entity.TaxJurisdiction = request.TaxJurisdiction;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var success = await _repository.UpdateAsync(entity, cancellationToken);
        if (!success)
        {
            throw new KeyNotFoundException("LegalEntity.Error.NotFound");
        }

        _logger.LogInformation("LegalEntity updated. Id={Id} TenantId={TenantId}", entity.Id, entity.TenantId);

        return true;
    }
}
