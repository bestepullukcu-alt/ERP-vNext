namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// WC-D3 (DCP-004 §2 D3) — how long ONE provider may take before the board goes on without it.
///
/// <para><b>Why this is configuration and not a literal.</b> The budget is an operator's judgement about how
/// long a reader should wait for a source, and it will differ between an in-process Mongo read (microseconds)
/// and the first network-backed provider (the case this whole slice exists for). A number compiled into the
/// handler could not be tuned per environment, and — measured in this repo more than once — could not be varied
/// by a test either, which is how "no test covers failure or timeout" became true.</para>
///
/// <para><b>Where it is bound:</b> <c>Diten.Platform.Infrastructure/DependencyInjection.cs</c>, from the
/// <see cref="SectionName"/> section, exactly like <see cref="WorkItemSlaOptions"/>.</para>
/// </summary>
public sealed class WorkAggregationResilienceOptions
{
    public const string SectionName = "WorkAggregation:Resilience";

    /// <summary>
    /// The per-provider budget. Applied to EACH provider call separately — it is not a budget for the whole
    /// aggregation, and the difference matters: with N providers the worst case is N × this value.
    ///
    /// <para>That N × exposure is deliberate and recorded (BL-303). The alternative — running the providers
    /// concurrently — is a separate decision with its own hazard: providers are registered <c>Scoped</c>, so
    /// concurrent calls would share one DI scope and one Mongo session across threads. Sequential is the
    /// behaviour that exists today, and this slice changes fault tolerance without also changing the
    /// concurrency model underneath it.</para>
    ///
    /// <para>The default is generous on purpose: both providers today are in-process and answer in
    /// milliseconds, so this timeout must not become a new source of missing rows on the day it ships. It is a
    /// ceiling on a hang, not a performance target.</para>
    ///
    /// <para>A non-positive value means the budget is already spent, and every provider is reported as
    /// <c>TIMEOUT</c> without being called to completion. That is not a supported production setting — it is
    /// the honest reading of "zero time allowed", and it is what lets the timeout test run with no wall-clock
    /// wait at all.</para>
    /// </summary>
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
