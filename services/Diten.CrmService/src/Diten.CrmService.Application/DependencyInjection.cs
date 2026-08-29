using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.CrmService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.PerformanceBehavior<,>));
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<Features.Account.IAccountCodeGenerator, Features.Account.AccountCodeGenerator>();

        // MOD-0151 FU01 — Territory reference validator composes the existing MOD-0048 consumer seams
        // (single-value validate, per-value attributes, whole-set catalog). No CRM-local seed / hardcoded fallback.
        services.AddScoped<Features.Territory.ITerritoryReferenceValidator, Features.Territory.TerritoryReferenceValidator>();

        // MOD-0151 FU08 — import engine (parse → validate → apply → record). Registered explicitly because the
        // handler is a thin forwarder and the engine is what the tests exercise directly.
        services.AddScoped<Features.Territory.ImportExport.TerritoryImportEngine>();

        // MOD-0165 FU03 — the single read-only frequency resolve seam (repo + deterministic engine). Both the FU03
        // HTTP endpoint and the MOD-0151 FU09B route-candidate reader consume THIS; no consumer copies the engine.
        services.AddScoped<Features.VisitFrequencyPolicy.Resolve.IVisitFrequencyPolicyResolver,
            Features.VisitFrequencyPolicy.Resolve.VisitFrequencyPolicyResolver>();

        // MOD-0164 FU02 — the single read-only consent/preference evaluation seam (repos + deterministic engine).
        // The FU02 HTTP endpoint and every future consumer (MOD-0155, MOD-0165 FU04, MOD-0167 consent filter) consume
        // THIS; no consumer copies the engine and none of them needs raw consent read access.
        services.AddScoped<Features.ConsentPreference.Evaluation.IConsentPreferenceEvaluator,
            Features.ConsentPreference.Evaluation.ConsentPreferenceEvaluator>();

        // MOD-0162 FU05 — the ONLY place a journey stage reaches the FU04 KnowledgePath, and it is READ-only: the
        // binding guard and the pinned / latest-published resolution both go through here, so the shipped
        // IKnowledgePathReader seam is never widened and no FU04 aggregate is ever mutated.
        services.AddScoped<Features.Knowledge.ContentEngagementJourney.ContentEngagementJourneyPathResolver>();

        return services;
    }
}
