using System.Reflection;
using System.Runtime.CompilerServices;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Application.Tests.Segmentation;
using Diten.CrmService.Application.Tests.VisitWorkspace;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// WP-CRM-SYNC-S0B B3 (F8-b closed, 2026-11-13) — the 40 commands F8-a marked (<c>IAuditableCommand</c>, the removed
/// <c>Diten.BuildingBlocks.Auditing</c>) now carry <see cref="ICentralAuditDeclaration"/>. Measured on the production
/// assembly, FOR EACH command: the marker, the Platform operation F8-a's <c>DescribeAudit()</c> named, the same entity
/// type, source module CRM (CT decision D-3), and where the entity id comes from. Removing one marker, or changing one
/// operation / entity type, turns its own row red.
/// <para>Then CRM's REAL pipeline (<c>AddApplication</c> + the shared recorder, <see cref="CapturedCentralAudit"/>) — the
/// cases the removed <c>CrmAuditForwardingPipelineTests</c> held, on the new contract (BL-501: a record for a SUCCEEDED
/// command only).</para>
/// </summary>
public sealed class CrmCentralAuditF8bTests
{
    private const string FromResponse = "response";
    private const string FromCommand = "command";
    private const string None = "none";

    /// <summary>Command · Platform operation · entity type · where the entity id comes from.</summary>
    public static TheoryData<string, string, string, string> F8bCommands() => new()
    {
        // MOD-0149 (CRM-ACC-EDIT)
        { "CreateAccountCommand", "Create", "Account", FromResponse },
        { "UpdateAccountCommand", "Update", "Account", FromCommand },
        // MOD-0168
        { "CreatePreOrderCommand", "Create", "PreOrder", FromResponse },
        { "UpdatePreOrderCommand", "Update", "PreOrder", FromCommand },
        { "DiscardPreOrderCommand", "LifecycleTransition", "PreOrder", FromCommand },
        { "CapturePreOrderSignatureCommand", "Update", "PreOrder", FromCommand },
        { "SubmitPreOrderCommand", "LifecycleTransition", "PreOrder", FromCommand },
        { "DecidePreOrderCommand", "LifecycleTransition", "PreOrder", FromCommand },
        { "CancelPreOrderCommand", "LifecycleTransition", "PreOrder", FromCommand },
        { "CopyPreOrderCommand", "Create", "PreOrder", FromResponse },
        { "TransmitPreOrderCommand", "Execute", "PreOrder", FromCommand },
        { "RecordPreOrderDepotResponseCommand", "Update", "PreOrder", FromCommand },
        { "UpdatePreOrderSettingsCommand", "Update", "PreOrderSettings", None },
        { "RunPreOrderTransmissionRetryCommand", "Execute", "PreOrder", FromCommand },
        // MOD-0156
        { "CreateFxRateCommand", "Create", "FxRate", FromResponse },
        { "UpdateFxRateCommand", "Update", "FxRate", FromCommand },
        { "ImportFxRatesCommand", "Create", "FxRate", None },
        { "UploadPriceListImportCommand", "Create", "PriceListImport", FromResponse },
        { "ConvertPriceListImportCommand", "LifecycleTransition", "PriceListImport", FromCommand },
        { "DiscardPriceListImportCommand", "LifecycleTransition", "PriceListImport", FromCommand },
        { "CreatePriceListCommand", "Create", "PriceList", FromResponse },
        { "UpdatePriceListCommand", "Update", "PriceList", FromCommand },
        { "CreatePriceListVersionCommand", "Create", "PriceList", FromResponse },
        { "DiscardPriceListCommand", "LifecycleTransition", "PriceList", FromCommand },
        { "SubmitPriceListCommand", "LifecycleTransition", "PriceList", FromCommand },
        { "DecidePriceListCommand", "LifecycleTransition", "PriceList", FromCommand },
        { "CreatePricingRuleCommand", "Create", "PricingRule", FromResponse },
        { "UpdatePricingRuleCommand", "Update", "PricingRule", FromCommand },          // Revision = false; the revision door: its own test
        { "RequestPricingRuleRetirementCommand", "Create", "PricingRule", FromResponse },
        { "DiscardPricingRuleDraftCommand", "LifecycleTransition", "PricingRule", FromCommand },
        { "SubmitPricingRuleCommand", "LifecycleTransition", "PricingRule", FromCommand },
        { "DecidePricingRuleCommand", "LifecycleTransition", "PricingRule", FromCommand },
        // MOD-0155 (W3L evidence scopes, W5 notification settings, W3E institution QR)
        { "UpsertVisitEvidencePolicyScopeCommand", "Update", "VisitEvidencePolicyLayer", None },
        { "ArchiveVisitEvidencePolicyScopeCommand", "Deactivate", "VisitEvidencePolicyLayer", None },
        { "UpdateVisitNotificationSettingsCommand", "Update", "VisitNotificationSettings", None },
        { "IssueVisitQrCommand", "Create", "AccountVisitQr", FromCommand },
        { "RevokeVisitQrCommand", "Revoke", "AccountVisitQr", FromCommand },
        { "GetVisitQrPayloadCommand", "Export", "AccountVisitQr", FromCommand },
        { "BulkIssueVisitQrCommand", "Create", "AccountVisitQr", None },
        { "BulkVisitQrPayloadCommand", "Export", "AccountVisitQr", None }
    };

    [Fact]
    public void The_F8b_slice_is_exactly_the_40_commands_F8a_marked()
    {
        var names = F8bCommands().Select(row => (string)row[0]).ToList();

        Assert.Equal(40, names.Count);
        Assert.Equal(40, names.Distinct(StringComparer.Ordinal).Count());
        // none of them is in an older slice's list (one command, one declaration test)
        Assert.Empty(names.Intersect(CrmCentralAuditDeclarationTests.KnowledgeCommands.Concat(CrmCentralAuditDeclarationTests.VisitCommands)
            .Concat(CrmCentralAuditDeclarationTests.VisitReportV2Commands)));
    }

    [Theory]
    [MemberData(nameof(F8bCommands))]
    public void Each_F8b_command_declares_the_crm_record_with_the_operation_and_entity_F8a_named(string name, string operation, string entityType, string idSource)
    {
        var type = Assert.Single(typeof(CrmCentralAudit).Assembly.GetTypes(), t => t.Name == name);
        Assert.True(typeof(ICentralAuditDeclaration).IsAssignableFrom(type), $"{name} carries no ICentralAuditDeclaration");
        Assert.False(typeof(ICentralAuditOutsideTransaction).IsAssignableFrom(type), $"{name}: CRM does not use RequireEnlistment");

        var command = (ICentralAuditDeclaration)RuntimeHelpers.GetUninitializedObject(type);
        var metadata = command.CentralAudit;

        Assert.Equal(CrmCentralAudit.Category, metadata.Category);
        Assert.Equal(operation, metadata.Operation);
        Assert.Equal(entityType, metadata.EntityType);
        Assert.Equal("CRM", metadata.SourceModule);
        Assert.Equal(CrmCentralAudit.SourceModule, metadata.SourceModule);
        Assert.True(CentralAuditPlatformValues.Operations.ContainsKey(metadata.Operation), $"{name}: operation {metadata.Operation} is not a Platform name");
        Assert.True(CentralAuditPlatformValues.IsKnown(metadata));

        var id = Guid.NewGuid();
        var (succeeded, failed) = Envelopes(type, id);
        switch (idSource)
        {
            case FromResponse:
                Assert.Equal(id, command.CentralAuditEntityId(succeeded));
                Assert.Null(command.CentralAuditEntityId(failed));
                Assert.Null(command.CentralAuditEntityId(null));
                break;
            case FromCommand:
                var property = type.GetProperty("Id") ?? type.GetProperty("AccountId") ?? throw new InvalidOperationException($"{name} carries no id");
                property.SetValue(command, id);
                Assert.Equal(id, command.CentralAuditEntityId(null));
                Assert.Equal(id, command.CentralAuditEntityId(failed));
                break;
            default:
                Assert.Null(command.CentralAuditEntityId(succeeded));
                Assert.Null(command.CentralAuditEntityId(null));
                break;
        }
    }

    [Fact]
    public void A_pricing_rule_revision_is_a_create_whose_id_is_the_new_drafts()
    {
        var (revised, created) = (Guid.NewGuid(), Guid.NewGuid());
        var revision = new UpdatePricingRuleCommand(revised, null!, Revision: true);
        var edit = new UpdatePricingRuleCommand(revised, null!);
        var dto = (PricingRuleDto)RuntimeHelpers.GetUninitializedObject(typeof(PricingRuleDto)) with { Id = created };

        Assert.Equal(("Create", "PricingRule"), (revision.CentralAudit.Operation, revision.CentralAudit.EntityType));
        Assert.Equal(created, revision.CentralAuditEntityId(Response<PricingRuleDto>.Success(dto)));
        Assert.Null(revision.CentralAuditEntityId(Response<PricingRuleDto>.Fail("nope")));
        Assert.Equal(("Update", "PricingRule"), (edit.CentralAudit.Operation, edit.CentralAudit.EntityType));
        Assert.Equal(revised, edit.CentralAuditEntityId(Response<PricingRuleDto>.Success(dto)));
    }

    /// <summary>The library F8-a lived in is gone from the CRM assemblies (nothing can mark a command the old way).</summary>
    [Fact]
    public void No_crm_assembly_references_the_removed_F8a_library()
    {
        Assembly[] assemblies =
        [
            typeof(CrmCentralAudit).Assembly, typeof(Program).Assembly, typeof(Diten.CrmService.Infrastructure.Audit.HttpCrmAuditPublisher).Assembly,
            typeof(Diten.CrmService.Persistence.DependencyInjection).Assembly
        ];

        Assert.All(assemblies, assembly =>
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "Diten.BuildingBlocks.Auditing"));
        Assert.DoesNotContain(typeof(CrmCentralAudit).Assembly.GetTypes().SelectMany(t => t.GetInterfaces()),
            i => i.Name is "IAuditableCommand" or "IAuditDetailsProvider");
    }

    // ── CRM's real pipeline ──────────────────────────────────────────────────────────────────────────────────────────

    private static readonly Guid Tenant = Guid.Parse("97c5aaaa-0000-4000-8000-000000000001");

    private readonly CapturedCentralAudit _audit = new();
    private readonly FakeNotificationSettings _settings = new();

    private ServiceProvider Build(Guid? tenantId, string? platformBaseUrl = null, string? platformKey = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CentralAuditOptions.PlatformSection}:BaseUrl"] = platformBaseUrl,
                [$"{CentralAuditOptions.PlatformSection}:InternalApiKey"] = platformKey
            })
            .Build();

        var tenant = new TenantContext();
        if (tenantId is { } id)
        {
            tenant.SetTenant(id);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        _audit.Register(services, configuration);
        services.AddSingleton<ITenantContext>(tenant);
        services.AddSingleton<IActorContext>(new FixedActorContext("rep-1"));
        services.AddSingleton<IVisitNotificationSettingsRepository>(_settings);
        services.AddSingleton<IVisitEvidenceAuditPublisher>(new RecordingAudit());
        services.AddTransient<IRequestHandler<UnmarkedProbeCommand, Response<int>>, UnmarkedProbeHandler>();
        services.AddTransient<IRequestHandler<GetVisitQrPayloadCommand, Response<VisitQrPayloadDto>>, PrintedPayloadHandler>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<T> Send<T>(ServiceProvider provider, IRequest<T> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private static UpdateVisitNotificationSettingsCommand Update(int? expectedVersion = null)
        => new(new VisitNotificationSettingsInput("pv.desk@example.test", null, true, null, expectedVersion));

    [Fact]
    public async Task A_marked_command_leaves_exactly_one_record_with_crm_values_and_no_mailbox()
    {
        using var provider = Build(Tenant);

        var response = await Send(provider, Update());

        Assert.True(response.IsSuccessful);
        var row = Assert.Single(_audit.Rows);
        Assert.Equal(nameof(UpdateVisitNotificationSettingsCommand), row.RequestType);
        Assert.Equal(Tenant, row.TenantId);
        Assert.Equal("Crm", row.Category);
        Assert.Equal(CentralAuditPlatformValues.Categories["Crm"], CentralAuditPlatformValues.Categories[row.Category]);
        Assert.Equal("Update", row.Operation);
        Assert.Equal(VisitNotificationSettingsHandlers.AuditEntity, row.EntityType);
        Assert.Equal("Diten.CrmService", row.SourceService);
        Assert.Equal("CRM", row.SourceModule); // CT decision D-3 (was MOD-0155)
        Assert.Null(row.EntityId);
        Assert.Equal(CentralAuditOutboxStatus.Pending, row.Status);
        var body = Assert.Single(_audit.Bodies);
        Assert.DoesNotContain("pv.desk@example.test", body, StringComparison.Ordinal);
        Assert.DoesNotContain("PvEmail", body, StringComparison.Ordinal); // not even the field names: the record names the entity (AUD-001 §3)
    }

    /// <summary>BL-501 contract (CT 2026-11-13): F8-a filed a refused command as "Failed"; the central log now holds
    /// succeeded commands only. The refusal is the caller's own 409.</summary>
    [Fact]
    public async Task A_refused_marked_command_leaves_no_record()
    {
        using var provider = Build(Tenant);

        var response = await Send(provider, Update(expectedVersion: 7)); // nothing stored yet: a stale version → 409

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(_audit.Rows);
    }

    [Fact]
    public async Task An_unmarked_command_and_a_query_leave_no_record()
    {
        using var provider = Build(Tenant);

        await Send(provider, new UnmarkedProbeCommand());
        await Send(provider, new GetVisitNotificationSettingsQuery());

        Assert.Empty(_audit.Rows);
    }

    /// <summary>Platform's address / key unset: the business command is never blocked and the record is not lost — it waits
    /// in the outbox (the dispatcher's warning and <c>/health/central-audit</c> Degraded say so:
    /// <see cref="CrmCentralAuditPipelineTests.Without_platform_settings_the_health_check_says_degraded"/>).</summary>
    [Fact]
    public async Task Unconfigured_platform_does_not_block_the_command_and_the_record_waits_in_the_outbox()
    {
        using var provider = Build(Tenant, platformBaseUrl: null, platformKey: null);

        var response = await Send(provider, Update());

        Assert.True(response.IsSuccessful);
        Assert.Equal(CentralAuditOutboxStatus.Pending, Assert.Single(_audit.Rows).Status);
    }

    /// <summary>No tenant, no record — never a default tenant (the recorder logs the error).</summary>
    [Fact]
    public async Task A_marked_command_without_a_resolved_tenant_files_nothing()
    {
        using var provider = Build(tenantId: null);

        await Send(provider, new GetVisitQrPayloadCommand(Guid.NewGuid(), "a4", 2));

        Assert.Empty(_audit.Rows);
    }

    [Fact]
    public async Task A_payload_print_is_recorded_as_an_export_of_the_institution_and_never_puts_the_payload_in_the_record()
    {
        using var provider = Build(Tenant);
        var command = new GetVisitQrPayloadCommand(Guid.NewGuid(), "a4", 2);

        var response = await Send(provider, command);

        Assert.Equal(PrintedPayloadHandler.Payload, response.Data!.Payload);
        var row = Assert.Single(_audit.Rows);
        Assert.Equal(("Export", "AccountVisitQr", (Guid?)command.AccountId), (row.Operation, row.EntityType, row.EntityId));
        Assert.DoesNotContain(PrintedPayloadHandler.Payload, Assert.Single(_audit.Bodies), StringComparison.Ordinal);
    }

    /// <summary>A succeeded and a failed <c>Response&lt;T&gt;</c> of the command's own response type, the data carrying
    /// <paramref name="id"/> where it has an <c>Id</c> (or is the id itself).</summary>
    private static (object Succeeded, object Failed) Envelopes(Type command, Guid id)
    {
        var envelope = command.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)).GetGenericArguments()[0];
        Assert.Equal(typeof(Response<>), envelope.GetGenericTypeDefinition());
        var dataType = envelope.GetGenericArguments()[0];

        object? data;
        if (dataType == typeof(Guid))
        {
            data = id;
        }
        else if (dataType == typeof(bool))
        {
            data = true;
        }
        else if (dataType.IsInterface)
        {
            data = null; // a list answer (bulk print): there is no single id to name
        }
        else
        {
            data = RuntimeHelpers.GetUninitializedObject(dataType);
            dataType.GetProperty("Id")?.SetValue(data, id);
        }

        var succeeded = data is null
            ? envelope.GetMethod(nameof(Response<int>.SuccessWithoutData))!.Invoke(null, [200])!
            : envelope.GetMethod(nameof(Response<int>.Success))!.Invoke(null, [data, 200])!;
        var failed = envelope.GetMethod(nameof(Response<int>.Fail), [typeof(string), typeof(int)])!.Invoke(null, ["nope", 400])!;
        return (succeeded, failed);
    }

    public sealed record UnmarkedProbeCommand : IRequest<Response<int>>;

    public sealed class UnmarkedProbeHandler : IRequestHandler<UnmarkedProbeCommand, Response<int>>
    {
        public Task<Response<int>> Handle(UnmarkedProbeCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Response<int>.Success(1));
    }

    /// <summary>Stands in for the QR print handler (the pipeline is what is measured): answers a payload.</summary>
    public sealed class PrintedPayloadHandler : IRequestHandler<GetVisitQrPayloadCommand, Response<VisitQrPayloadDto>>
    {
        public const string Payload = "SIGNED-PAYLOAD-VALUE";

        public Task<Response<VisitQrPayloadDto>> Handle(GetVisitQrPayloadCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Response<VisitQrPayloadDto>.Success(
                new VisitQrPayloadDto(request.AccountId, "Eczane", "ACC-1", 3, Payload, DateTimeOffset.UtcNow)));
    }
}
