using System.Text;
using Diten.PlanningService.Application.Features.DemandPlanning.Behaviors;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.Contracts;
using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

public static class DemandPlanningServiceCollectionExtensions
{
    private static readonly string[] PermissionKeys =
    [
        "demand.plans.read", "demand.cycles.create", "demand.history-imports.import",
        "demand.history-integrations.import", "demand.history-imports.review",
        "demand.forecast-policies.update", "demand.forecast-runs.create",
        "demand.drafts.create", "demand.drafts.update", "demand.plans.review",
        "demand.plans.publish", "demand.plans.invalidate", "demand.plans.consume",
        "demand.audit.read"
    ];

    public static IServiceCollection AddDemandPlanningFeature(
        this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:Secret"];
        var issuer = configuration["JwtSettings:Issuer"];
        var audience = configuration["JwtSettings:Audience"];
        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < 32 ||
            string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException(
                "PlanningService requires a JWT secret of at least 32 bytes, issuer and audience.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build();
            foreach (var key in PermissionKeys)
            {
                options.AddPolicy(key, policy =>
                {
                    policy.RequireAuthenticatedUser().RequireClaim("permission", key);
                    if (key == "demand.plans.publish")
                        policy.RequireAssertion(_ => false); // Publish remains closed until provenance and SoD are implemented.
                });
            }
        });

        var assembly = typeof(IProductMasterV1Reader).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddTransient<ManualDraftWorkflow>();
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));

        services.AddSingleton<DemandPlanningMongoContext>();
        services.AddSingleton<ManualDraftMongoStore>();
        services.AddSingleton<IManualDraftStore>(sp => sp.GetRequiredService<ManualDraftMongoStore>());
        services.AddSingleton<IInternalPublishAuthority, UnconfiguredPublishAuthority>();
        services.AddSingleton<PublishedRevisionMongoStore>();
        services.AddSingleton<IRollbackDraftStore, RollbackDraftMongoStore>();
        services.AddSingleton<IInternalPublishedRevisionStore>(sp =>
            sp.GetRequiredService<PublishedRevisionMongoStore>());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IInternalSnapshotReadAuthority, UnconfiguredSnapshotReadAuthority>();
        services.AddSingleton<IInternalPublishedSnapshotReader, PublishedSnapshotPageReader>();
        services.AddSingleton<IInternalRevisionStatusAuthority, UnconfiguredRevisionStatusAuthority>();
        services.AddSingleton<IInternalAuthoritativeRevisionStatusReader, AuthoritativeRevisionStatusReader>();
        services.AddSingleton<IInternalCurrentPublishedAuthority, UnconfiguredCurrentPublishedAuthority>();
        services.AddSingleton<IInternalCurrentPublishedReader, CurrentPublishedRevisionReader>();
        services.AddSingleton<IInternalInvalidationAuthority, UnconfiguredInvalidationAuthority>();
        services.AddSingleton<IInternalRevisionInvalidator, InvalidationMongoStore>();
        services.AddSingleton<IInternalInvalidatedHistoryAuthority, UnconfiguredInvalidatedHistoryAuthority>();
        services.AddSingleton<IInternalInvalidatedHistoryReader, InvalidatedHistoryReader>();
        services.AddSingleton<IManualDraftAuthority, UnconfiguredManualDraftAuthority>();
        services.AddSingleton<IPlanningCycleStore, PlanningCycleMongoStore>();
        services.AddSingleton<IPlanningCycleAuthority, UnconfiguredPlanningCycleAuthority>();
        services.AddSingleton<IHistoryImportBatchStore, DemandHistoryImportMongoStore>();
        services.AddSingleton<IHistoryImportReviewAuditStore, DemandHistoryReviewAuditMongoStore>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, DemandReviewAuthorizationResultHandler>();
        services.AddSingleton<IHistoryImportFileParser, CsvHistoryImportFileParser>();
        services.AddSingleton<UnconfiguredHistoryImportReferences>();
        services.AddSingleton<IHistoryImportScopeAuthority>(sp => sp.GetRequiredService<UnconfiguredHistoryImportReferences>());
        services.AddSingleton<IHistoryRowReferenceChecker>(sp => sp.GetRequiredService<UnconfiguredHistoryImportReferences>());
        services.AddSingleton<UnconfiguredFrozenContractReaders>();
        services.AddSingleton<IProductMasterV1Reader>(sp => sp.GetRequiredService<UnconfiguredFrozenContractReaders>());
        services.AddSingleton<ILocationV1Reader>(sp => sp.GetRequiredService<UnconfiguredFrozenContractReaders>());
        services.AddSingleton<IDispatchHistoryReader>(sp => sp.GetRequiredService<UnconfiguredFrozenContractReaders>());
        return services;
    }
}
