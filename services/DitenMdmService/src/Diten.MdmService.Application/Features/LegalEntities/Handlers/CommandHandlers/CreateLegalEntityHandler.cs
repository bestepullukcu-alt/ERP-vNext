using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Commands;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.CommandHandlers;

public sealed class CreateLegalEntityHandler : IRequestHandler<CreateLegalEntityCommand, CreateLegalEntityResult>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateLegalEntityHandler> _logger;

    public CreateLegalEntityHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<CreateLegalEntityHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CreateLegalEntityResult> Handle(
        CreateLegalEntityCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new LegalEntity
        {
            Title = request.Title,
            TaxOffice = request.TaxOffice,
            TaxNumber = request.TaxNumber,
            Email = request.Email,
            Phone = request.Phone,
            Website = request.Website,
            Address = request.Address,
            CompanyType = request.CompanyType,
            Sector = request.Sector,
            ContactPerson = request.ContactPerson,
            PrimaryCurrency = request.PrimaryCurrency,
            DefaultTimeZone = request.DefaultTimeZone,
            ParentLegalEntityId = request.ParentLegalEntityId,
            DefaultCommunicationLanguage = request.DefaultCommunicationLanguage,
            OrganizationRole = request.OrganizationRole,
            LogoUrl = request.LogoUrl,
            FiscalYearStart = request.FiscalYearStart,
            RegistrationDate = request.RegistrationDate,
            EffectiveFromDate = request.EffectiveFromDate,
            TaxJurisdiction = request.TaxJurisdiction,
            TenantId = _tenantContext.TenantId,
            IsActive = true
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "LegalEntity created. Id={Id} TenantId={TenantId}",
            created.Id,
            created.TenantId);

        return new CreateLegalEntityResult(
            created.Id,
            created.Title,
            created.TaxNumber,
            created.TenantId,
            created.CreatedAt);
    }
}
