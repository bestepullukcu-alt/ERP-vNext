using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Api.CentralAudit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// WP-CRM-SYNC-S0B B3 (F8-b) — the central audit of a test world that builds CRM's pipeline by hand
/// (<c>AddApplication</c> + this): the SHARED recorder and behavior (<c>AddCentralAudit</c>, innermost — register it right
/// after <c>AddApplication</c>, like <c>Program.cs</c>) and CRM's REAL <see cref="CrmCentralAuditScope"/> over the world's
/// own <c>ITenantContext</c>; only the outbox is in memory. What a test reads back is what the recorder wrote: one row per
/// SUCCEEDED marked command, none for a refused one, a query or an unmarked command (BL-501).
/// <para>A world has no HTTP request, so its rows are filed as the System actor (B0b) — the caller-bound fields are
/// measured on the real host in <see cref="CrmCentralAuditPipelineTests"/> / <see cref="CrmCentralAuditScopeTests"/>.</para>
/// </summary>
internal sealed class CapturedCentralAudit
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public InMemoryCentralAuditOutboxStore Store { get; } = new();

    /// <summary>In the order the commands succeeded.</summary>
    public IReadOnlyList<CentralAuditOutboxRecord> Rows => Store.Rows.ToList();

    /// <summary>Each row as JSON (camelCase: <c>requestType</c>, <c>sourceModule</c>, <c>operation</c>, <c>entityType</c> …)
    /// — everything the record holds, for "this text never reaches the record" assertions.</summary>
    public IReadOnlyList<string> Bodies => Rows.Select(row => JsonSerializer.Serialize(row, Wire)).ToList();

    public IEnumerable<string> RequestTypes => Rows.Select(row => row.RequestType);

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddCentralAudit<CrmCentralAuditScope>(configuration, "Diten.CrmService");
        services.AddSingleton(Store);
        services.AddScoped<ICentralAuditOutboxStore>(_ => Store);
    }
}
