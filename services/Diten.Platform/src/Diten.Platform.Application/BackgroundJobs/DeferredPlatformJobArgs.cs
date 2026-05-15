namespace Diten.Platform.Application.BackgroundJobs;

public sealed record DeferredPlatformJobArgs(string OwnerModule, string Reason);
