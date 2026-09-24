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
            // SCMM-09 audit bundle — concept graph events forwarded with SourceModule "MOD-0162".
            services.AddScoped<Application.Features.Knowledge.Concept.IKnowledgeConceptAuditPublisher>(
                sp => sp.GetRequiredService<HttpCrmAuditPublisher>());
            // SCMM-12 audit — claim / composition events forwarded with SourceModule "CAND-CAP-0011".
            services.AddScoped<Application.Features.ContentComposition.IContentCompositionAuditPublisher>(
                sp => sp.GetRequiredService<HttpCrmAuditPublisher>());
        }
        else
        {
            services.AddScoped<IAccountAuditPublisher, LoggingAccountAuditPublisher>();
            services.AddScoped<Application.Features.Contact.IContactAuditPublisher, LoggingContactAuditPublisher>();
            // SCMM-09 audit bundle — structured-logging fallback for concept graph events.
            services.AddScoped<Application.Features.Knowledge.Concept.IKnowledgeConceptAuditPublisher,
                LoggingKnowledgeConceptAuditPublisher>();
            // SCMM-12 audit — structured-logging fallback for claim / composition events.
            services.AddScoped<Application.Features.ContentComposition.IContentCompositionAuditPublisher,
                LoggingContentCompositionAuditPublisher>();
        }

        // SCMM-11 (CAND-CAP-0011) — eligibility gate: the in-process port (thin over the resolver query) and the RM4
        // evaluation-log writer (structured-logging default; a durable/audit sink is a follow).
        services.AddScoped<Application.Features.ContentComposition.Eligibility.IEligibilityEvaluationPort,
            Application.Features.ContentComposition.Eligibility.EligibilityEvaluationPort>();
        services.AddScoped<Application.Features.ContentComposition.Eligibility.IEligibilityEvaluationLogWriter,
            ContentComposition.LoggingEligibilityEvaluationLogWriter>();

        // MOD-0150 FU05 — read-only consent/preference seam. Default is the no-op reader (MOD-0164 not built yet);
        // it fabricates no consent state and makes no network call. A config-gated HTTP reader replaces it when MOD-0164 ships.
        services.AddScoped<Application.Features.ConsentPreference.IContactConsentPreferenceReader,
            ConsentPreference.NullContactConsentPreferenceReader>();

        // SCMM-16B (CAND-CAP-0011) — ContentSetRevision render pipeline. The PDF renderer is stateless (singleton). The
        // artifact store is a typed Gateway client that forwards the caller's token to the MOD-0262-FU01 document
        // repository (fail-closed: a store failure fails the render). Platform/FU01 is consumed as-is, never modified.
        services.AddSingleton<
            Application.Features.ContentComposition.ContentSetRevisions.Rendering.IContentSetRevisionRenderer,
            ContentComposition.Rendering.PdfSharpContentSetRevisionRenderer>();
        services.AddHttpClient<
            Application.Features.ContentComposition.ContentSetRevisions.Rendering.IContentArtifactStore,
            ContentComposition.Rendering.HttpContentArtifactStore>();

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

        // MOD-0155 FU03 — the configured route-optimization placeholders (09:00–18:00 field day + lunch, road factor,
        // assumed field speed). Singleton: configuration is read once at startup. NOT derived from CycleCapacity
        // (DailyWorkMinutes has no start/end/lunch structure); HR/MOD-0288 is the additive future source (D-WORKINGHOURS).
        services.AddSingleton<
            Application.Features.RouteOptimization.IRouteOptimizationDefaultsProvider,
            RouteOptimization.ConfigurationRouteOptimizationDefaultsProvider>();

        // WP-SEG-DETAILS6 — S2S display-name reader onto AuthService's internal/users/display-names endpoint. It resolves
        // the segment timeline's CreatedBy/ActivatedBy/UpdatedBy provenance ids to display names in ONE bulk call, using
        // the shared internal API key (a direct call: the internal endpoints are NOT behind the Gateway JWT surface).
        // Fail-closed — an unconfigured/unreachable AuthService leaves the names absent and the read still succeeds.
        services.Configure<Auth.AuthServiceOptions>(configuration.GetSection(Auth.AuthServiceOptions.SectionName));
        services.AddHttpClient<
            Application.Common.IUserDisplayNameResolver,
            Auth.AuthUserDisplayNameClient>();

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
