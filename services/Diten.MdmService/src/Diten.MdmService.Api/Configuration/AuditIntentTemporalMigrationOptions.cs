using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Diten.MdmService.Api.Configuration;

public sealed class AuditIntentTemporalMigrationOptions
{
    public const string SectionName = "AuditIntentTemporalMigration";
    public bool Enabled { get; init; }
    public string LeaseOwner { get; init; } = string.Empty;
    public int BatchSize { get; init; } = 100;
    public int LeaseSeconds { get; init; } = 60;
    public bool VerifyCompletion { get; init; }
    public bool ActivateCutover { get; init; }
    public string SelectedIndexEvidenceFingerprint { get; init; } = string.Empty;
}

public static class AuditIntentTemporalMigrationCommandLine
{
    public const string ExactArgument = "--run-audit-intent-temporal-migration";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        var count = arguments.Count(argument => string.Equals(argument, ExactArgument, StringComparison.Ordinal));
        if (count > 1)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_COMMAND_DUPLICATE");
        }
        return count == 1;
    }

    public static AuditIntentTemporalMigrationRequest ValidateAndCreateRequest(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_ENVIRONMENT_NOT_ALLOWED");
        }

        var options = configuration.GetSection(AuditIntentTemporalMigrationOptions.SectionName)
            .Get<AuditIntentTemporalMigrationOptions>()
            ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_CONFIGURATION_INVALID");
        if (!options.Enabled
            || string.IsNullOrWhiteSpace(options.LeaseOwner)
            || options.LeaseOwner.Length > 128
            || options.BatchSize is < 1 or > 1000
            || options.LeaseSeconds is < 10 or > 900
            || (options.VerifyCompletion || options.ActivateCutover)
               && !string.Equals(
                   options.SelectedIndexEvidenceFingerprint,
                   AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint,
                   StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_CONFIGURATION_INVALID");
        }

        return new AuditIntentTemporalMigrationRequest(
            options.LeaseOwner.Trim(),
            options.BatchSize,
            TimeSpan.FromSeconds(options.LeaseSeconds),
            options.VerifyCompletion,
            options.ActivateCutover,
            options.SelectedIndexEvidenceFingerprint);
    }
}
