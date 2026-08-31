using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.WorkAggregation;

/// <summary>
/// WC-D1 — WHERE A CONFIGURATION ROW BECOMES A BOUND PROVIDER. This is the entire cost of adding a module that
/// lives in another service.
///
/// <para>Two rows produce two providers and two dispatchers from ONE class each, which is the guarantee the round
/// was scoped around and which <c>HttpWorkItemBridgeTests</c> measures by binding a two-row configuration and
/// counting what comes out of the container. No file in this repository may name a specific remote module.</para>
/// </summary>
public static class RemoteWorkItemProviderRegistration
{
    /// <summary>
    /// Bind <c>WorkAggregation:RemoteProviders</c> and register one <see cref="HttpWorkItemProvider"/> and one
    /// <see cref="HttpWorkItemActionDispatcher"/> per row, into the same two <c>IEnumerable</c> collections the
    /// in-process providers already register into. Nothing in <c>GetMyWorkItemsHandler</c> or
    /// <c>WorkItemsController</c> changes — that is what the provider seam was built for.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A malformed or duplicated row STOPS THE SERVICE. A bad address that only showed up as a permanently
    /// unavailable source on a reader's board would be a typo reported as an outage, months later, to the wrong
    /// person.
    /// </exception>
    public static IServiceCollection AddRemoteWorkItemProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rows = configuration
            .GetSection(RemoteWorkItemProviderOptions.SectionName)
            .Get<List<RemoteWorkItemProviderOptions>>() ?? [];

        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            errors.AddRange(row.Validate(index));

            // A duplicate code would bind two providers under one name: the board would show both sets of items
            // and every write would go to whichever dispatcher the container happened to enumerate first.
            if (!string.IsNullOrWhiteSpace(row.ProviderCode) && !seen.Add(row.ProviderCode))
            {
                errors.Add(
                    $"'{RemoteWorkItemProviderOptions.SectionName}': provider code '{row.ProviderCode}' "
                    + "is configured more than once.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration error in remote work-item providers:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }

        // Registered even with zero rows, so the shape of the container does not depend on configuration and the
        // gateway is resolvable for tests and for the first row an operator adds.
        services.AddScoped<RemoteWorkItemGateway>();

        /*
         * ONE named client for every row. Its timeout is disabled deliberately: the only deadline that may apply
         * is WorkAggregation:Resilience:ProviderTimeout, applied by the aggregation loop on the read path and by
         * the dispatcher on the write path. A 100-second default sitting underneath would be a second, invisible
         * answer to the operator's one question about how long a reader waits.
         *
         * ⚠ NO tenant DelegatingHandler here, and it is not an omission — one was tried and MEASURED not to work
         * (2026-08-28). IHttpClientFactory caches its handler chain in its own scope, so such a handler's
         * request-scoped ITenantContext is never resolved and the header is silently dropped. RemoteWorkItemGateway
         * writes the header itself, from the request scope. See its class comment, and TenantOnTheWire for the one
         * answer to which tenant may travel. The shared handler this warns about was detached from the last two
         * clients in BL-311 and deleted outright in BL-316 — do not re-create it.
         */
        services.AddHttpClient(RemoteWorkItemGateway.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        foreach (var row in rows)
        {
            var captured = row;

            services.AddScoped<IWorkItemProvider>(sp => new HttpWorkItemProvider(
                captured,
                sp.GetRequiredService<RemoteWorkItemGateway>(),
                sp.GetRequiredService<ILogger<HttpWorkItemProvider>>()));

            services.AddScoped<IWorkItemActionDispatcher>(sp => new HttpWorkItemActionDispatcher(
                captured,
                sp.GetRequiredService<RemoteWorkItemGateway>(),
                sp.GetRequiredService<IOptions<Application.Features.WorkAggregation.Services
                    .WorkAggregationResilienceOptions>>(),
                sp.GetRequiredService<ILogger<HttpWorkItemActionDispatcher>>()));
        }

        return services;
    }
}
