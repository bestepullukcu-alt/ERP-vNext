using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Infrastructure.Audit;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Infrastructure.Middleware;
using Diten.CrmService.Infrastructure.ReferenceValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.CrmService.Infrastructure;

/// <summary>
/// CRM infrastructure wiring (MOD-0149-PREREQ scaffold): tenant context (server-side resolve),
/// HttpContext accessor and the generic permission-authorization plumbing so future MOD-0149
/// endpoints can be guarded with <c>[HasPermission("crm.account....")]</c>. NO Account/CRM business
/// adapters, audit clients or permission seed are registered here.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddHttpContextAccessor();
        // MOD-0150 FU07 — provenance actor (CreatedBy/UpdatedBy) resolved from the caller principal, never a payload.
        services.AddScoped<IActorContext, HttpActorContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<Application.Features.Territory.ITerritoryLifecycleAuditPublisher,
            Audit.LoggingTerritoryLifecycleAuditPublisher>();

        // MOD-0021 audit seam. Default is the structured-logging seam; set Crm:Audit:Mode=http to forward
        // Account/Contact/import/export events to the governed audit append contract over the Gateway (fail-soft).
        if (string.Equals(configuration["Crm:Audit:Mode"], "http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<HttpCrmAuditPublisher>();
            services.AddScoped<IAccountAuditPublisher>(sp => sp.GetRequiredService<HttpCrmAuditPublisher>());
            services.AddScoped<Application.Features.Contact.IContactAuditPublisher>(sp => sp.GetRequiredService<HttpCrmAuditPublisher>());
        }
        else
        {
            services.AddScoped<IAccountAuditPublisher, LoggingAccountAuditPublisher>();
            services.AddScoped<Application.Features.Contact.IContactAuditPublisher, LoggingContactAuditPublisher>();
        }

        // MOD-0150 FU05 — read-only consent/preference seam. Default is the no-op reader (MOD-0164 not built yet);
        // it fabricates no consent state and makes no network call. A config-gated HTTP reader replaces it when MOD-0164 ships.
        services.AddScoped<Application.Features.ConsentPreference.IContactConsentPreferenceReader,
            ConsentPreference.NullContactConsentPreferenceReader>();

        // MOD-0167 FU02 - class-X criterion VALUE proof (MDM global product / product / brand) over the Gateway.
        // Deliberately cacheless, 3s budget, one transient retry; 404 makes the rule un-authorable (400) and an
        // unreachable dependency is a 503 with nothing persisted. It never derives membership.
        services.AddHttpClient<
            Application.Features.Segmentation.Catalog.ISegmentProductReferenceValidator,
            Segmentation.MdmSegmentProductReferenceValidator>();

        // MOD-0167 FU04 - the same fail-closed profile for the StrategyTemplate product/SKU bindings (MDM
        // GlobalProduct + Gsku) over the Gateway. Cacheless, 3s budget, one transient retry; 404 makes the binding
        // un-authorable (400) and an unreachable dependency is a 503 with nothing persisted. No brand path exists here
        // (D-BRAND) and product-to-SKU containment is deliberately NOT checked (D-SKU-LINK).
        services.AddHttpClient<
            Application.Features.StrategyTemplate.Binding.IStrategyTemplateProductReferenceValidator,
            StrategyTemplate.MdmStrategyTemplateReferenceValidator>();

        // MOD-0165 FU07 - the CyclePeriod legal-entity scope. Same fail-closed profile as the working calendar's own
        // validator and MOD-0167 FU02's: cacheless, 3s budget, one transient retry, always through the Gateway. It runs
        // BEFORE any insert, so 404 / not-referenceable is a 400 and an unreachable MDM is a 503 with nothing written.
        // A third copy on purpose - the three live in different services, and sharing a library would couple CrmService
        // to Platform.
        services.AddHttpClient<
            Application.Features.CyclePeriod.Services.ICyclePeriodLegalEntityValidator,
            CyclePeriod.MdmCyclePeriodLegalEntityValidator>();

        // The authoring lookup behind the scope selector. Deliberately separate from the validator above: choosing an
        // option never substitutes for proving the reference at save time.
        services.AddHttpClient<
            Application.Features.CyclePeriod.Read.ICyclePeriodLegalEntityCatalog,
            CyclePeriod.MdmCyclePeriodLegalEntityCatalog>();

        // The narrow READ window onto MOD-0151 Territory that the business-unit picker needs. Registering this seam -
        // rather than letting a CyclePeriod handler take ITerritoryModelRepository - is what makes "FU07 never writes
        // to Territory" structural instead of a convention.
        services.AddScoped<
            Application.Features.CyclePeriod.Read.ITerritoryBusinessUnitCatalog,
            CyclePeriod.TerritoryBusinessUnitCatalog>();

        // MOD-0155 FU06 - the READ-ONLY working-day seam onto CAND-CAP-0008. Same fail-closed transport profile as
        // the validators above (cacheless, 3s budget, one transient retry, always through the Gateway), but a
        // deliberately different failure MEANING: this is a calculation input rather than a write proof, so an
        // unreachable calendar makes the ESTIMATE unavailable and never blocks authoring a capacity.
        // It targets /api/platform/working-calendars/overrides/resolve, NOT the country-layer /resolve: the latter is
        // an admin path the Gateway 400s on X-Tenant-Id and 403s for tenant tokens (see the class comment).
        services.AddHttpClient<
            Application.Features.CycleCapacity.Read.IWorkingDayCounter,
            CycleCapacity.WorkingCalendarWorkingDayCounter>();

        // MOD-0155 FU06 - the configured capacity defaults (8h day, interim FTE average). Singleton: configuration is
        // read once at startup, and the values are then COPIED onto each new capacity so an old estimate stays
        // reproducible after a setting changes.
        services.AddSingleton<
            Application.Features.CycleCapacity.Services.ICycleCapacityDefaultsProvider,
            CycleCapacity.ConfigurationCycleCapacityDefaultsProvider>();

        services.AddHttpClient<IReferenceDataValidator, GatewayReferenceDataValidator>();
        // MOD-0150 FU04 — the same Gateway validator also reads per-value attributes (relationship-type metadata).
        services.AddScoped<Application.Common.ReferenceValidation.IReferenceMetadataReader>(
            sp => (Application.Common.ReferenceValidation.IReferenceMetadataReader)sp.GetRequiredService<IReferenceDataValidator>());
        // MOD-0150 Import/Export Task 1 — and the whole published value list, for the workbook ReferenceData helper
        // sheet + in-cell dropdowns. Same Gateway consumer; no CRM local seed.
        services.AddScoped<Application.Common.ReferenceValidation.IReferenceDataCatalogReader>(
            sp => (Application.Common.ReferenceValidation.IReferenceDataCatalogReader)sp.GetRequiredService<IReferenceDataValidator>());

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}
