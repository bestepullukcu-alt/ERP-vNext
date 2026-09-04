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

        // MOD-0155 FU01 — the four read-only PlannedVisit provenance probes. Each is a thin in-process wrapper over an
        // already-registered seam (frequency resolver, consent evaluator, journey reader, contact-availability repo):
        // it never re-implements an engine and never makes an HTTP self-call (§19.3/5). Scoped like every write-path
        // component the create/update/confirm handlers compose.
        services.AddScoped<Features.PlannedVisit.Provenance.PlannedVisitFrequencyProbe>();
        services.AddScoped<Features.PlannedVisit.Provenance.PlannedVisitConsentProbe>();
        services.AddScoped<Features.PlannedVisit.Provenance.PlannedVisitJourneyProbe>();
        services.AddScoped<Features.PlannedVisit.Provenance.PlannedVisitAvailabilityProbe>();
        services.AddScoped<Features.PlannedVisit.Handlers.CommandHandlers.PlannedVisitWriteGuards>();

        // MOD-0155 FU03 — the in-process route + time-window scheduler seam, mirroring the resolver seams. The FU05
        // packing engine (in-process) and the FU03 dry-run preview endpoint's handler both consume THIS; no consumer
        // re-implements the greedy heuristic and there is no HTTP self-call. It performs no writes, holds no repository,
        // and its only dependency is the config defaults provider (registered in Infrastructure). The pure
        // TimeWindowInsertionEngine + HaversineTravelModel it delegates to need no registration — they are constructed,
        // not injected — which is what keeps them pure and swappable (F-SOLVER: a real solver implements THIS interface).
        services.AddScoped<Features.RouteOptimization.IRouteOptimizer,
            Features.RouteOptimization.GreedyTimeWindowRouteOptimizer>();

        // MOD-0155 FU04 — the in-process Visit Content Sequence resolver seam. The read-only preview endpoint's handler
        // and the FU05 packing engine (in-process) both consume THIS; there is exactly one logic path (AC-EP-2). It
        // persists nothing and only READS the already-registered strategy / journey / segment / content-linkage seams
        // plus the CycleCapacity repo, then delegates the arithmetic to the pure FU06B ActivityTimeBudgetCalculator.
        services.AddScoped<Features.VisitContentSequence.VisitContentSequenceResolver>();

        // MOD-0155 FU05 — the MicroTarget Visit Planning Engine + its read-only selection helpers. The engine is a
        // sealed coordinator: it CONSUMES FU03 (IRouteOptimizer), FU04 (VisitContentSequenceResolver), FU06B
        // (CycleCapacityEstimator), MOD-0165 (frequency resolver via FrequencyExtendPlanner), MOD-0164 (consent),
        // MOD-0150 (availability) and MOD-0149/0151 (accounts / territory) IN-PROCESS via DI — no HTTP self-call, no
        // re-implemented algorithm (D8). It writes FU01 atoms only through the atomic IPlanningSessionApplyUnitOfWork
        // (registered in Persistence). Every helper is scoped like the write-path components it composes.
        services.AddScoped<Features.VisitPlanning.EligibleContactSelector>();
        services.AddScoped<Features.VisitPlanning.PharmacyExpander>();
        services.AddScoped<Features.VisitPlanning.TerritoryGate>();
        services.AddScoped<Features.VisitPlanning.FrequencyExtendPlanner>();
        services.AddScoped<Features.VisitPlanning.VisitPlanningEngine>();

        return services;
    }
}
