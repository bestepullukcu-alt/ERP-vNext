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
        var entity = await _repository.GetByIdAsync(request.Id, _tenantContext.TenantId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("LegalEntity not found. Id={Id} TenantId={TenantId}", request.Id, _tenantContext.TenantId);
            return false;
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

        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("LegalEntity updated. Id={Id} TenantId={TenantId}", entity.Id, entity.TenantId);

        return true;
    }
}
