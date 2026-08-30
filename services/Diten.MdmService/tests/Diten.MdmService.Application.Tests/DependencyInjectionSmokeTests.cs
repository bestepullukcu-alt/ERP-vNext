using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.Audit;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Audit;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class DependencyInjectionSmokeTests
{
    [Fact]
    public void Temporal_migration_is_scoped_required_and_never_hosted()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs");

        Assert.Contains(
            "AddScoped<IAuditIntentTemporalMigrationRepository, AuditIntentTemporalMigrationRepository>()",
            source,
            StringComparison.Ordinal);
        Assert.Contains("AddScoped<AuditIntentTemporalMigrationRunner>()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<AuditIntentTemporalMigrationRunner>", source, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(AuditIntentTemporalMigrationRunner)));
        var productionConstructor = Assert.Single(
            typeof(AuditIntentDeliveryRepository).GetConstructors(),
            constructor => constructor.GetParameters().Length == 4);
        Assert.Contains(productionConstructor.GetParameters(), parameter =>
            parameter.ParameterType == typeof(IAuditIntentTemporalMigrationRepository));
    }

    [Fact]
    public void Temporal_migration_is_default_disabled_and_requires_exact_cli_argument()
    {
        Assert.False(new AuditIntentTemporalMigrationOptions().Enabled);
        Assert.True(AuditIntentTemporalMigrationCommandLine.IsRequested(
            [AuditIntentTemporalMigrationCommandLine.ExactArgument]));
        Assert.False(AuditIntentTemporalMigrationCommandLine.IsRequested([]));
        Assert.Equal(
            "AUDIT_INTENT_TEMPORAL_MIGRATION_COMMAND_DUPLICATE",
            Assert.Throws<InvalidOperationException>(() => AuditIntentTemporalMigrationCommandLine.IsRequested(
                [AuditIntentTemporalMigrationCommandLine.ExactArgument, AuditIntentTemporalMigrationCommandLine.ExactArgument])).Message);
        Assert.False(AuditIntentTemporalMigrationCommandLine.IsRequested(
            [AuditIntentTemporalMigrationCommandLine.ExactArgument.ToUpperInvariant()]));
    }

    [Fact]
    public void Trusted_source_delivery_dependencies_are_required_and_worker_is_default_disabled()
    {
        var program = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs");
        var infrastructure = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs");

        Assert.Contains("AddHostedService<AuditIntentDeliveryWorker>()", program, StringComparison.Ordinal);
        Assert.Contains("AuditIntentDeliveryWorkerOptions.SectionName", program, StringComparison.Ordinal);
        Assert.Contains(nameof(ITrustedSourceAuditServiceIdentityProvider), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(ITrustedSourceAuditIntentClient), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(AuditIntentDeliveryProcessor), infrastructure, StringComparison.Ordinal);
        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(AuditIntentDeliveryWorker)));
        Assert.False(new AuditIntentDeliveryWorkerOptions().Enabled);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
