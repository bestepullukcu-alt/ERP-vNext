namespace Diten.MdmService.Api.Configuration;

public sealed class GlobalProductCorrectionWorkflowWorkerOptions
{
    public const string SectionName = "GlobalProductCorrectionWorkflowWorker";
    public bool Enabled { get; set; }
    public int LeaseSeconds { get; set; } = 60;
    public int RetryDelaySeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;

    public bool IsValid() => LeaseSeconds is >= 10 and <= 900
        && RetryDelaySeconds is >= 1 and <= 3600
        && PollIntervalSeconds is >= 1 and <= 3600
        && BatchSize is >= 1 and <= 100;
}
