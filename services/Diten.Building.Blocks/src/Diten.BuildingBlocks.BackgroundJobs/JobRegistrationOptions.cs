namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed record JobRegistrationOptions(
    string? JobName = null,
    string Owner = "MOD-0026",
    string Queue = "default",
    int MaxRetryAttempts = 5);
