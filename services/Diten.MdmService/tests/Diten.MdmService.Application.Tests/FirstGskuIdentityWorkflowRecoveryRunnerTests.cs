using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FirstGskuIdentityWorkflowRecoveryRunnerTests
{
    [Fact]
    public async Task Disabled_runner_and_worker_are_no_op_without_resolving_runtime_dependencies()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new FirstGskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FirstGskuIdentityWorkflowWorkerOptions()),
            TimeProvider.System,
            NullLogger<FirstGskuIdentityWorkflowRecoveryRunner>.Instance);
        var worker = new FirstGskuIdentityWorkflowRecoveryWorker(
            runner,
            Options.Create(new FirstGskuIdentityWorkflowWorkerOptions()),
            NullLogger<FirstGskuIdentityWorkflowRecoveryWorker>.Instance);

        var result = await runner.RunCycleAsync();
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(new FirstGskuIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0), result);
    }

    [Fact]
    public void Options_are_default_disabled_and_reject_ambiguous_or_malformed_configuration()
    {
        Assert.False(new FirstGskuIdentityWorkflowOptions().Enabled);
        Assert.False(new FirstGskuIdentityWorkflowWorkerOptions().Enabled);

        Assert.Throws<InvalidOperationException>(() => new FirstGskuIdentityWorkflowOptions
        {
            Enabled = true,
            TemplateId = Guid.NewGuid(),
            TemplateCode = "GSKU-IDENTITY",
            CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "IDENTITY_APPROVAL"
        }.ToStartConfiguration());
        Assert.Throws<InvalidOperationException>(() => new FirstGskuIdentityWorkflowWorkerOptions
        {
            Enabled = true,
            LeaseOwner = " worker "
        }.EnsureValidWhenEnabled());
    }

    [Fact]
    public void Enabled_options_freeze_exact_sorted_template_and_candidate_snapshot()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var configuration = new FirstGskuIdentityWorkflowOptions
        {
            Enabled = true,
            TemplateCode = "GSKU-IDENTITY",
            CandidatePrincipalIds = [second, first],
            ReasonCode = "IDENTITY_APPROVAL",
            CommentRequired = true,
            EvidenceRequired = true,
            DueAfterSeconds = 3600
        }.ToStartConfiguration();

        Assert.Null(configuration.TemplateId);
        Assert.Equal("GSKU-IDENTITY", configuration.TemplateCode);
        Assert.Equal([first, second], configuration.CandidatePrincipalIds);
        Assert.Equal(TimeSpan.FromHours(1), configuration.DueAfter);
    }

    [Theory]
    [InlineData("--run-first-gsku-identity-workflow-recovery", true)]
    [InlineData("--other", false)]
    public void Command_line_accepts_only_the_exact_argument(string argument, bool expected) =>
        Assert.Equal(expected, FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested([argument]));

    [Fact]
    public void Command_line_rejects_case_alias_suffix_and_duplicate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--RUN-FIRST-GSKU-IDENTITY-WORKFLOW-RECOVERY"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--run-first-gsku-identity-workflow-recovery=true"]));
        Assert.Throws<InvalidOperationException>(() =>
            FirstGskuIdentityWorkflowRecoveryCommandLine.IsRequested([
                FirstGskuIdentityWorkflowRecoveryCommandLine.ExactArgument,
                FirstGskuIdentityWorkflowRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Command_line_runner_propagates_caller_cancellation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new FirstGskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FirstGskuIdentityWorkflowWorkerOptions()),
            TimeProvider.System,
            NullLogger<FirstGskuIdentityWorkflowRecoveryRunner>.Instance);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            FirstGskuIdentityWorkflowRecoveryCommandLine.RunAsync(runner, source.Token));
    }

    [Fact]
    public async Task Worker_survives_a_failed_cycle_and_runs_the_next_cycle()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var settings = new FirstGskuIdentityWorkflowWorkerOptions
        {
            Enabled = true,
            LeaseOwner = "test-worker",
            PollIntervalSeconds = 1,
            LeaseSeconds = 10,
            RetryDelaySeconds = 1
        };
        var runner = new FirstGskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            TimeProvider.System,
            NullLogger<FirstGskuIdentityWorkflowRecoveryRunner>.Instance);
        var logger = new CaptureLogger<FirstGskuIdentityWorkflowRecoveryWorker>();
        var worker = new ResilientWorker(runner, Options.Create(settings), logger);

        await worker.StartAsync(CancellationToken.None);
        await worker.SecondCycle.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.CycleCount >= 2);
        Assert.Contains(logger.Messages, message => message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("sensitive-value-must-not-be-logged", StringComparison.Ordinal));
    }

    private sealed class ResilientWorker(
        FirstGskuIdentityWorkflowRecoveryRunner runner,
        IOptions<FirstGskuIdentityWorkflowWorkerOptions> options,
        ILogger<FirstGskuIdentityWorkflowRecoveryWorker> logger)
        : FirstGskuIdentityWorkflowRecoveryWorker(
            runner,
            options,
            logger)
    {
        public TaskCompletionSource SecondCycle { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int CycleCount { get; private set; }

        protected override Task RunCycleAsync(CancellationToken cancellationToken)
        {
            CycleCount++;
            if (CycleCount == 1)
            {
                throw new InvalidOperationException("sensitive-value-must-not-be-logged");
            }

            SecondCycle.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
