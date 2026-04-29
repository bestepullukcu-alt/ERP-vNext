using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class ImportModuleCatalogCommandHandler : IRequestHandler<ImportModuleCatalogCommand, ModuleCatalogImportResultDto>
{
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;
    private readonly IModuleDefinitionRepository _moduleRepository;
    private readonly ICurrentUserContext _currentUser;

    public ImportModuleCatalogCommandHandler(
        IDomainLandscapeRepository domainRepository,
        ISuitePlatformRepository suiteRepository,
        ICapabilityGroupRepository capabilityRepository,
        IModuleDefinitionRepository moduleRepository,
        ICurrentUserContext currentUser)
    {
        _domainRepository = domainRepository;
        _suiteRepository = suiteRepository;
        _capabilityRepository = capabilityRepository;
        _moduleRepository = moduleRepository;
        _currentUser = currentUser;
    }

    public async Task<ModuleCatalogImportResultDto> Handle(ImportModuleCatalogCommand request, CancellationToken cancellationToken)
    {
        var actor = ResolveActor(_currentUser);
        var domains = (await _domainRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var suites = (await _suiteRepository.GetAllAsync(cancellationToken)).ToDictionary(x => BuildSuiteKey(x.DomainLandscapeId, x.Code), StringComparer.OrdinalIgnoreCase);
        var capabilities = (await _capabilityRepository.GetAllAsync(cancellationToken)).ToDictionary(x => BuildCapabilityKey(x.SuitePlatformId, x.Code), StringComparer.OrdinalIgnoreCase);
        var modules = (await _moduleRepository.GetAllAsync(cancellationToken)).ToDictionary(x => NormalizeModuleId(x.ModuleId), StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failedRows = new List<ModuleCatalogImportRowErrorDto>();

        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var rowNumber = index + 1;
            var validation = ValidateImportRow(row);
            if (validation != null)
            {
                failedRows.Add(new ModuleCatalogImportRowErrorDto(rowNumber, row.ModuleId, row.ModuleName, validation));
                continue;
            }

            try
            {
                var domainName = row.DomainLandscape!.Trim();
                var suiteName = row.SuitePlatform!.Trim();
                var capabilityName = row.CapabilityGroup!.Trim();
                var moduleId = NormalizeModuleId(row.ModuleId!);
                var moduleName = row.ModuleName!.Trim();
                var domainCode = ModuleCatalogCodeNormalizer.NormalizeToCode(domainName);
                var suiteCode = ModuleCatalogCodeNormalizer.NormalizeToCode(suiteName);
                var capabilityCode = ModuleCatalogCodeNormalizer.NormalizeToCode(capabilityName);
                var status = ParseStatus(row.Status);
                var dependencyGate = NormalizeNullable(row.DependencyGate);
                var deliveryOutcome = NormalizeNullable(row.DeliveryOutcome);
                var placement = NormalizeNullable(row.Placement);
                var supportModel = NormalizeNullable(row.SupportModel);
                var isPlatformCore = row.IsPlatformCore ?? false;
                var isTenantAssignable = isPlatformCore ? false : (row.IsTenantAssignable ?? true);

                if (!domains.TryGetValue(domainCode, out var domain))
                {
                    domain = await _domainRepository.CreateAsync(new DomainLandscape
                    {
                        Code = domainCode,
                        Name = domainName,
                        IsActive = true,
                        CreatedBy = actor
                    }, cancellationToken);
                    domains[domainCode] = domain;
                }

                var suiteKey = BuildSuiteKey(domain.Id, suiteCode);
                if (!suites.TryGetValue(suiteKey, out var suite))
                {
                    suite = await _suiteRepository.CreateAsync(new SuitePlatform
                    {
                        Code = suiteCode,
                        Name = suiteName,
                        DomainLandscapeId = domain.Id,
                        IsActive = true,
                        CreatedBy = actor
                    }, cancellationToken);
                    suites[suiteKey] = suite;
                }

                var capabilityKey = BuildCapabilityKey(suite.Id, capabilityCode);
                if (!capabilities.TryGetValue(capabilityKey, out var capability))
                {
                    capability = await _capabilityRepository.CreateAsync(new CapabilityGroup
                    {
                        Code = capabilityCode,
                        Name = capabilityName,
                        DomainLandscapeId = domain.Id,
                        SuitePlatformId = suite.Id,
                        IsActive = true,
                        CreatedBy = actor
                    }, cancellationToken);
                    capabilities[capabilityKey] = capability;
                }

                if (!modules.TryGetValue(moduleId, out var existingModule))
                {
                    var createdEntity = await _moduleRepository.CreateAsync(new ModuleDefinition
                    {
                        ModuleId = moduleId,
                        ModuleName = moduleName,
                        DomainLandscapeId = domain.Id,
                        SuitePlatformId = suite.Id,
                        CapabilityGroupId = capability.Id,
                        DependencyGate = dependencyGate,
                        DeliveryOutcome = deliveryOutcome,
                        Placement = placement,
                        SupportModel = supportModel,
                        Status = status,
                        IsPlatformCore = isPlatformCore,
                        IsTenantAssignable = isTenantAssignable,
                        CreatedBy = actor
                    }, cancellationToken);

                    modules[moduleId] = createdEntity;
                    created++;
                    continue;
                }

                var changed = ApplyModuleUpdate(
                    existingModule,
                    domain.Id,
                    suite.Id,
                    capability.Id,
                    dependencyGate,
                    deliveryOutcome,
                    placement,
                    supportModel,
                    moduleName,
                    status,
                    isPlatformCore,
                    isTenantAssignable,
                    actor);
                if (!changed)
                {
                    skipped++;
                    continue;
                }

                await _moduleRepository.UpdateAsync(existingModule, cancellationToken);
                updated++;
            }
            catch (Exception ex)
            {
                failedRows.Add(new ModuleCatalogImportRowErrorDto(rowNumber, row.ModuleId, row.ModuleName, ex.Message));
            }
        }

        return new ModuleCatalogImportResultDto(created, updated, skipped, failedRows.Count, failedRows);
    }
}
