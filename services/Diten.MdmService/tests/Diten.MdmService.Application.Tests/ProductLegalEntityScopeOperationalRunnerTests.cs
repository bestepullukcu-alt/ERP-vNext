using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeOperationalRunnerTests
{
    private static readonly Guid TenantId = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("31000000-0000-0000-0000-000000000002");
    private static readonly Guid CommandId = Guid.Parse("31000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activation_snapshot_header_is_exactly_bound_and_schema_drift_fails_closed()
    {
        var rolloutId = Guid.NewGuid();
        var command = Guid.NewGuid();
        var request = new ProductLegalEntityScopeInventorySnapshotRequest(
            TenantId, rolloutId, ProductLegalEntityScopeRolloutMode.Preparation, 0,
            "ActivateEnforced", command, ActorId, "OWNER_APPROVED", Now);
        var fence = new ProductLegalEntityScopeActivationFence
        {
            Token = new string('F', 64), State = ProductLegalEntityScopeAdmissionState.Closing,
            Action = request.Action, CommandId = command, ActorId = ActorId,
            ReasonCode = request.ReasonCode, AcquiredAtUtc = Now
        };
        var keys = Assert.IsAssignableFrom<IReadOnlyList<string>>(typeof(ProductLegalEntityScopeOperationalRunner)
            .GetMethod("ExpectedInventoryKeys", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null));
        ProductLegalEntityScopeInventorySnapshot Snapshot(string schema) => ValidSnapshot(
            Now,
            string.Join('\n', keys.Select(key => $"{key}={Value(key, schema)}")) + "\n");
        var validate = typeof(ProductLegalEntityScopeOperationalRunner).GetMethod(
            "ValidateBoundActivationSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rejected = Assert.Throws<TargetInvocationException>(() => validate.Invoke(
            null, [Snapshot("2"), request, fence, Now]));
        Assert.IsType<InvalidOperationException>(rejected.InnerException);
        var valid = Snapshot("1");
        validate.Invoke(null, [valid, request, fence, Now]);

        foreach (var malformedPayload in new[]
        {
            valid.CanonicalPayload + "\n",
            valid.CanonicalPayload.Replace("\n", "\n\n", StringComparison.Ordinal),
            valid.CanonicalPayload.Replace("\n", "\r\n", StringComparison.Ordinal)
        })
        {
            var malformed = new ProductLegalEntityScopeInventorySnapshot
            {
                ObservedAtUtc = valid.ObservedAtUtc,
                CanonicalPayload = malformedPayload,
                StableFactsHash = valid.StableFactsHash,
                SnapshotHash = valid.SnapshotHash
            };
            var malformedError = Assert.Throws<TargetInvocationException>(() => validate.Invoke(
                null, [malformed, request, fence, Now]));
            Assert.IsType<InvalidOperationException>(malformedError.InnerException);
            Assert.Equal("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID", malformedError.InnerException!.Message);
        }

        string Value(string key, string schema) => key switch
        {
            "schemaVersion" => schema, "tenantId" => TenantId.ToString("D"),
            "rolloutStateId" => rolloutId.ToString("D"), "rolloutMode" => "1", "rolloutVersion" => "0",
            "action" => request.Action, "commandId" => command.ToString("D"), "actorId" => ActorId.ToString("D"),
            "reasonCodeBase64" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.ReasonCode)),
            "observedUtcTicks" => Now.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ when key.EndsWith("Hash", StringComparison.Ordinal) || key.EndsWith(".hash", StringComparison.Ordinal) => new string('C', 64),
            _ => "0"
        };
    }

    [Fact]
    public void Transition_audit_proof_requires_exactly_one_of_intent_or_receipt()
    {
        using var harness = CreateHarness(OptionsFor("Inspect"));
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId, Guid.NewGuid(), ActorId, Now);
        var intentId = Guid.NewGuid();
        rollout.LastTransitionIntentId = intentId;
        rollout.LastTransitionEvidenceHash = new string('E', 64);
        rollout.AuditIntents.Add(new LocalAuditIntent { IntentId = intentId });
        rollout.AuditIntentReceipts.Add(new LocalAuditIntentReceipt { IntentId = intentId });

        var runnerType = typeof(ProductLegalEntityScopeOperationalRunner);
        var factsType = runnerType.GetNestedType(
            "ProductLegalEntityScopeOperationalFacts", BindingFlags.NonPublic)!;
        var facts = Activator.CreateInstance(factsType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: ["ActivateEnforced", TenantId, ActorId, CommandId, rollout.Id, rollout.Version, "OWNER_APPROVED"],
            culture: null)!;
        var determine = runnerType.GetMethod("DetermineAuditPending", BindingFlags.Instance | BindingFlags.NonPublic)!;

        AssertProofInvalid();
        rollout.AuditIntents.Clear();
        rollout.AuditIntentReceipts.Clear();
        AssertProofInvalid();

        void AssertProofInvalid()
        {
            var error = Assert.Throws<TargetInvocationException>(() =>
                determine.Invoke(harness.Runner, [rollout, facts]));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Equal("PRODUCT_SCOPE_OPERATION_AUDIT_PROOF_INVALID", error.InnerException!.Message);
        }
    }

    [Fact]
    public async Task Activate_and_suspend_exact_replay_append_one_intent_and_exact_receipt_clears_AuditPending()
    {
        var activation = OptionsFor("ActivateEnforced");
        activation.ActorId = ActorId; activation.CommandId = CommandId;
        activation.ExpectedRolloutStateId = Guid.NewGuid(); activation.ExpectedRolloutVersion = 0;
        activation.ReasonCode = "OWNER_APPROVED";
        using var harness = CreateHarness(activation);
        harness.Rollout.Seed(ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId, Guid.NewGuid(), ActorId, Now));
        activation.ExpectedRolloutStateId = harness.Rollout.State!.Id;

        var completed = await harness.Runner.RunAsync();
        var replay = await harness.Runner.RunAsync();
        Assert.True(completed.AuditPending); Assert.True(replay.AuditPending);
        Assert.Single(harness.Rollout.State!.AuditIntents);
        harness.Rollout.AddExactReceipt(Now);
        Assert.False((await harness.Runner.RunAsync()).AuditPending);
        Assert.Empty(harness.Rollout.State.AuditIntents);
        Assert.Single(harness.Rollout.State.AuditIntentReceipts);

        var suspension = OptionsFor("SuspendFailClosed");
        suspension.ActorId = ActorId; suspension.CommandId = Guid.NewGuid();
        suspension.ExpectedRolloutStateId = harness.Rollout.State.Id;
        suspension.ExpectedRolloutVersion = harness.Rollout.State.Version;
        suspension.ReasonCode = "EMERGENCY_SUSPENSION";
        using var suspendedHarness = CreateHarness(suspension);
        suspendedHarness.Rollout.Seed(harness.Rollout.State);
        var suspended = await suspendedHarness.Runner.RunAsync();
        Assert.Equal("FailClosedSuspended", suspended.Rollout.Mode);
        Assert.True(suspended.AuditPending);
        Assert.Single(suspendedHarness.Rollout.State!.AuditIntents);
        Assert.Single(suspendedHarness.Rollout.State.AuditIntentReceipts);
    }

    [Fact]
    public async Task Suspension_rejects_tampered_snapshot_before_transition_commit()
    {
        var suspension = OptionsFor("SuspendFailClosed");
        suspension.ActorId = ActorId;
        suspension.CommandId = Guid.NewGuid();
        suspension.ExpectedRolloutStateId = Guid.NewGuid();
        suspension.ExpectedRolloutVersion = 1;
        suspension.ReasonCode = "EMERGENCY_SUSPENSION";
        using var harness = CreateHarness(suspension);
        var enforced = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId, Guid.NewGuid(), ActorId, Now);
        enforced.Id = suspension.ExpectedRolloutStateId.Value;
        enforced.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        enforced.Version = suspension.ExpectedRolloutVersion.Value;
        harness.Rollout.Seed(enforced);
        harness.Readiness.ReturnTamperedSnapshot = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID", error.Message);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, harness.Rollout.State!.Mode);
        Assert.Empty(harness.Rollout.State.AuditIntents);
    }

    [Theory]
    [InlineData("after-fence")]
    [InlineData("after-snapshot-1")]
    [InlineData("after-snapshot-2")]
    [InlineData("after-bind")]
    [InlineData("after-commit")]
    public async Task Activation_crash_at_each_checkpoint_resumes_by_exact_same_command_without_duplicate_intent(
        string failurePoint)
    {
        var options = OptionsFor("ActivateEnforced");
        options.ActorId = ActorId; options.CommandId = CommandId;
        options.ExpectedRolloutStateId = Guid.NewGuid(); options.ExpectedRolloutVersion = 0;
        options.ReasonCode = "OWNER_APPROVED";
        using var harness = CreateHarness(options);
        harness.Rollout.Seed(ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId, Guid.NewGuid(), ActorId, Now));
        options.ExpectedRolloutStateId = harness.Rollout.State!.Id;
        harness.Rollout.FailurePoint = failurePoint;
        harness.Readiness.FailurePoint = failurePoint;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());
        var recovered = await harness.Runner.RunAsync();

        Assert.Equal("Enforced", recovered.Rollout.Mode);
        Assert.True(recovered.AuditPending);
        Assert.Single(harness.Rollout.State!.AuditIntents);
        Assert.Equal(CommandId, harness.Rollout.State.LastTransitionCommandId);
    }


    [Fact]
    public async Task NonDevelopment_FailsBeforeScopeOrRepositoryResolution()
    {
        var createdScopes = 0;
        var runner = new ProductLegalEntityScopeOperationalRunner(
            new Environment("Production"),
            Options.Create(OptionsFor("Inspect")),
            new ThrowingScopeFactory(() => createdScopes++),
            new FixedTimeProvider(Now));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED", error.Message);
        Assert.Equal(0, createdScopes);
    }

    [Theory]
    [InlineData(false, "Inspect", "PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID")]
    [InlineData(true, "", "PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID")]
    [InlineData(true, "inspect", "PRODUCT_SCOPE_OPERATIONAL_ACTION_NOT_AUTHORIZED")]
    [InlineData(true, "ActivateEnforced", "PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID")]
    [InlineData(true, "SuspendFailClosed", "PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID")]
    public async Task InvalidConfiguration_FailsBeforeScopeResolution(
        bool enabled,
        string action,
        string expectedCode)
    {
        var createdScopes = 0;
        var options = OptionsFor(action);
        options.Enabled = enabled;
        var runner = new ProductLegalEntityScopeOperationalRunner(
            new Environment(Environments.Development),
            Options.Create(options),
            new ThrowingScopeFactory(() => createdScopes++),
            new FixedTimeProvider(Now));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());

        Assert.Equal(expectedCode, error.Message);
        Assert.Equal(0, createdScopes);
    }

    [Fact]
    public async Task Inspect_Uninitialized_IsReadOnlyAndDoesNotClaimActivationReadiness()
    {
        var harness = CreateHarness(OptionsFor("Inspect"));

        var result = await harness.Runner.RunAsync();

        Assert.Equal("Uninitialized", result.Rollout.Mode);
        Assert.Null(result.Rollout.Id);
        Assert.False(result.WasCreated);
        Assert.Equal(3, result.Completeness.EligibleGlobalProductCount);
        Assert.Equal(1, result.Completeness.ConfiguredGlobalProductCount);
        Assert.True(result.Completeness.HasMore);
        Assert.Equal(0, harness.Rollout.CreateCalls);
        Assert.Equal(TenantId, harness.Tenant.TenantId);
    }

    [Fact]
    public async Task BootstrapPreparation_CreatesOnceAndExactReplayReturnsSameState()
    {
        var options = OptionsFor("BootstrapPreparation");
        var harness = CreateHarness(options);

        var created = await harness.Runner.RunAsync();
        var replayed = await harness.Runner.RunAsync();

        Assert.True(created.WasCreated);
        Assert.False(replayed.WasCreated);
        Assert.Equal(created.Rollout.Id, replayed.Rollout.Id);
        Assert.Equal(CommandId, replayed.Rollout.CreationCommandId);
        Assert.Equal(ActorId, replayed.Rollout.CreatedByActorId);
        Assert.Equal(1, harness.Rollout.CreateCalls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BootstrapPreparation_DriftFailsClosed(bool commandDrift, bool actorDrift)
    {
        var options = OptionsFor("BootstrapPreparation");
        var harness = CreateHarness(options);
        await harness.Runner.RunAsync();
        options.CommandId = commandDrift ? Guid.NewGuid() : CommandId;
        options.ActorId = actorDrift ? Guid.NewGuid() : ActorId;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_ROLLOUT_BOOTSTRAP_CONFLICT", error.Message);
        Assert.Equal(1, harness.Rollout.CreateCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BootstrapPreparation_AmbiguousOutcomeRequiresExactPersistedProof(bool persistExactProof)
    {
        var harness = CreateHarness(OptionsFor("BootstrapPreparation"));
        harness.Rollout.ReturnAmbiguousCreate = true;
        harness.Rollout.PersistAmbiguousCreate = persistExactProof;

        if (persistExactProof)
        {
            var result = await harness.Runner.RunAsync();
            Assert.False(result.WasCreated);
            Assert.Equal(CommandId, result.Rollout.CreationCommandId);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());
            Assert.Equal("PRODUCT_SCOPE_RECONCILIATION_REQUIRED", error.Message);
        }
    }

    [Fact]
    public async Task BootstrapPreparation_DuplicateRaceReplayDoesNotClaimCreation()
    {
        var harness = CreateHarness(OptionsFor("BootstrapPreparation"));
        harness.Rollout.RecoveredState = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId,
            CommandId,
            ActorId,
            Now);

        var result = await harness.Runner.RunAsync();

        Assert.False(result.WasCreated);
        Assert.Equal(harness.Rollout.RecoveredState.Id, result.Rollout.Id);
        Assert.Equal(1, harness.Rollout.CreateCalls);
    }

    [Fact]
    public async Task BootstrapPreparation_MalformedPersistedStateFailsClosedWithoutCreate()
    {
        var harness = CreateHarness(OptionsFor("BootstrapPreparation"));
        harness.Rollout.Seed(ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId,
            CommandId,
            ActorId,
            Now), withInvalidVersion: true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_ROLLOUT_BOOTSTRAP_CONFLICT", error.Message);
        Assert.Equal(0, harness.Rollout.CreateCalls);
    }

    [Fact]
    public async Task Inspect_ReadinessFailureMapsToDeterministicFailure()
    {
        var harness = CreateHarness(OptionsFor("Inspect"));
        harness.Readiness.ThrowOnInspect = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_READINESS_UNAVAILABLE", error.Message);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutResolution()
    {
        var harness = CreateHarness(OptionsFor("Inspect"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Runner.RunAsync(source.Token));
        Assert.False(harness.Tenant.IsResolved);
    }

    [Fact]
    public void CommandLineArgument_IsExactOrdinal()
    {
        Assert.True(ProductLegalEntityScopeOperationalCommandLine.IsRequested(
            [ProductLegalEntityScopeOperationalCommandLine.RunArgument]));
        Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_ARGUMENT_INVALID", Assert.Throws<InvalidOperationException>(() =>
            ProductLegalEntityScopeOperationalCommandLine.IsRequested(
                ["--RUN-PRODUCT-LEGAL-ENTITY-SCOPE-OPERATIONAL"])).Message);
        Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_ARGUMENT_INVALID", Assert.Throws<InvalidOperationException>(() =>
            ProductLegalEntityScopeOperationalCommandLine.IsRequested(
                ["--run-product-legal-entity-scope-operational=true"])).Message);
    }

    [Fact]
    public async Task CommandLineRunner_PropagatesCallerCancellationBeforeResolution()
    {
        var harness = CreateHarness(OptionsFor("Inspect"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProductLegalEntityScopeOperationalCommandLine.RunAsync(harness.Runner, source.Token));

        Assert.False(harness.Tenant.IsResolved);
    }

    private static Harness CreateHarness(ProductLegalEntityScopeOperationalOptions options)
    {
        var services = new ServiceCollection();
        var tenant = new TenantContext();
        var rollout = new RolloutRepository();
        services.AddSingleton<ITenantContext>(tenant);
        services.AddSingleton(rollout);
        services.AddScoped<IProductLegalEntityScopeRolloutStateRepository>(provider =>
        {
            Assert.True(provider.GetRequiredService<ITenantContext>().IsResolved);
            return provider.GetRequiredService<RolloutRepository>();
        });
        services.AddScoped<IGlobalProductRepository>(_ => new GlobalProductRepositoryStub());
        var readiness = new ReadinessRepositoryStub(rollout);
        services.AddScoped<IProductLegalEntityScopeOperationalReadinessRepository>(_ => readiness);
        var provider = services.BuildServiceProvider();
        var runner = new ProductLegalEntityScopeOperationalRunner(
            new Environment(Environments.Development),
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now));
        return new(runner, tenant, rollout, readiness, provider);
    }

    private static ProductLegalEntityScopeOperationalOptions OptionsFor(string action) => new()
    {
        Enabled = true,
        Action = action,
        TenantId = TenantId,
        ActorId = string.Equals(action, "BootstrapPreparation", StringComparison.Ordinal) ? ActorId : null,
        CommandId = string.Equals(action, "BootstrapPreparation", StringComparison.Ordinal) ? CommandId : null
    };

    private sealed record Harness(
        ProductLegalEntityScopeOperationalRunner Runner,
        TenantContext Tenant,
        RolloutRepository Rollout,
        ReadinessRepositoryStub Readiness,
        ServiceProvider Provider) : IDisposable
    {
        public void Dispose() => Provider.Dispose();
    }

    private sealed class RolloutRepository : IProductLegalEntityScopeRolloutStateRepository
    {
        private ProductLegalEntityScopeRolloutState? _state;
        public int CreateCalls { get; private set; }
        public bool ReturnAmbiguousCreate { get; set; }
        public bool PersistAmbiguousCreate { get; set; }
        public ProductLegalEntityScopeRolloutState? RecoveredState { get; set; }
        public string? FailurePoint { get; set; }
        public ProductLegalEntityScopeRolloutState? State => _state;

        public void Seed(ProductLegalEntityScopeRolloutState state, bool withInvalidVersion = false)
        {
            _state = state;
            if (withInvalidVersion)
            {
                _state.Version = -1;
            }
        }

        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_state?.CreationCommandId == creationCommandId ? _state : null);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
            ProductLegalEntityScopeRolloutState state,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            if (RecoveredState is not null)
            {
                _state = RecoveredState;
                return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(true, _state));
            }
            if (ReturnAmbiguousCreate)
            {
                _state = PersistAmbiguousCreate ? state : null;
                return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(
                    false,
                    _state,
                    WriteOutcomeAmbiguous: true));
            }
            _state = state;
            return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(true, state));
        }

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
            ProductLegalEntityScopeRolloutState state,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductLegalEntityScopeFenceResult> AcquireFenceAsync(
            ProductLegalEntityScopeActivationFence requested, Guid expectedRolloutStateId,
            int expectedVersion, ProductLegalEntityScopeRolloutMode expectedMode,
            CancellationToken cancellationToken = default)
        {
            if (_state is null || _state.Id != expectedRolloutStateId || _state.Version != expectedVersion
                || _state.Mode != expectedMode)
                return Task.FromResult(new ProductLegalEntityScopeFenceResult(false, null, _state, "PRODUCT_SCOPE_ACTIVATION_FENCE_CONFLICT"));
            _state.ActiveFence = requested;
            ThrowOnce("after-fence");
            return Task.FromResult(new ProductLegalEntityScopeFenceResult(true, requested, _state));
        }

        public Task<bool> BindFenceSnapshotsAsync(string fenceToken,
            ProductLegalEntityScopeInventorySnapshot first, ProductLegalEntityScopeInventorySnapshot second,
            CancellationToken cancellationToken = default)
        {
            if (_state?.ActiveFence?.Token != fenceToken) return Task.FromResult(false);
            _state.ActiveFence.State = ProductLegalEntityScopeAdmissionState.Quiesced;
            _state.LastInventorySnapshot = second;
            ThrowOnce("after-bind");
            return Task.FromResult(true);
        }

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(
            string fenceToken, ProductLegalEntityScopeRolloutState requested, int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (_state?.ActiveFence?.Token != fenceToken || _state.Version != expectedVersion)
                return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(false, _state, VersionConflict: true));
            requested.ActiveFence = null;
            _state = requested;
            if (string.Equals(FailurePoint, "after-commit", StringComparison.Ordinal))
            {
                FailurePoint = null;
                return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(
                    false, _state, WriteOutcomeAmbiguous: true));
            }
            return Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(true, _state));
        }

        private void ThrowOnce(string point)
        {
            if (!string.Equals(FailurePoint, point, StringComparison.Ordinal)) return;
            FailurePoint = null;
            throw new InvalidOperationException($"simulated-{point}");
        }

        public void AddExactReceipt(DateTimeOffset now)
        {
            var intent = _state!.AuditIntents.Single(intent => intent.IntentId == _state.LastTransitionIntentId);
            const string acknowledgement = "central-ack";
            _state.AuditIntentReceipts.Add(new LocalAuditIntentReceipt
            {
                IntentId = intent.IntentId, TenantId = _state.TenantId,
                IdempotencyKey = intent.IdempotencyKey, SourceService = AuditIntentContract.SourceService,
                CentralAcknowledgement = acknowledgement,
                CentralIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(
                    _state.TenantId, intent.IntentId, AuditIntentDeliveryProcessor.RequiredContractVersion),
                ContractVersion = AuditIntentDeliveryProcessor.RequiredContractVersion,
                AcknowledgedAt = now, DeliveredAt = now, CompactedAt = now,
                CompactReceiptReference = acknowledgement, EvidenceHash = _state.LastTransitionEvidenceHash!
            });
            _state.AuditIntents.Remove(intent);
        }
    }

    private sealed class GlobalProductRepositoryStub : IGlobalProductRepository
    {
        public Task<GlobalProductScopeCompletenessInventory> GetProductLegalEntityScopeCompletenessInventoryAsync(
            DateTimeOffset serverNowUtc,
            int maximumMissingItems,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GlobalProductScopeCompletenessInventory(3, 1, [Guid.NewGuid()]));

        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ReadinessRepositoryStub(RolloutRepository rollout)
        : IProductLegalEntityScopeOperationalReadinessRepository
    {
        public bool ThrowOnInspect { get; set; }
        public string? FailurePoint { get; set; }
        public bool ReturnTamperedSnapshot { get; set; }
        private int _snapshotCalls;

        public Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(
            int maximumSampleSize,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnInspect)
            {
                throw new InvalidOperationException("simulated-read-failure");
            }
            var state = rollout.State;
            var fact = state is null
                ? null
                : new ProductLegalEntityScopeOperationalRolloutFact(
                    state.Id,
                    state.Mode,
                    state.Version,
                    state.CreationCommandId,
                    state.CreatedByActorId);
            return Task.FromResult(new ProductLegalEntityScopeOperationalReadiness(fact, [], []));
        }

        public Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(
            ProductLegalEntityScopeInventorySnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            _snapshotCalls++;
            var point = $"after-snapshot-{_snapshotCalls}";
            if (string.Equals(FailurePoint, point, StringComparison.Ordinal))
            {
                FailurePoint = null;
                throw new InvalidOperationException($"simulated-{point}");
            }
            var keys = Assert.IsAssignableFrom<IReadOnlyList<string>>(typeof(ProductLegalEntityScopeOperationalRunner)
                .GetMethod("ExpectedInventoryKeys", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null));
            string Value(string key) => key switch
            {
                "schemaVersion" => "1", "tenantId" => request.TenantId.ToString("D"),
                "rolloutStateId" => request.RolloutStateId.ToString("D"),
                "rolloutMode" => ((int)request.RolloutMode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "rolloutVersion" => request.RolloutVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "action" => request.Action, "commandId" => request.CommandId.ToString("D"),
                "actorId" => request.ActorId.ToString("D"),
                "reasonCodeBase64" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.ReasonCode)),
                "observedUtcTicks" => request.ObservedAtUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ when key.EndsWith("Hash", StringComparison.Ordinal) || key.EndsWith(".hash", StringComparison.Ordinal) => new string('C', 64),
                _ => "0"
            };
            var snapshot = ValidSnapshot(
                request.ObservedAtUtc,
                string.Join('\n', keys.Select(key => $"{key}={Value(key)}")) + "\n");
            if (ReturnTamperedSnapshot)
                snapshot.CanonicalPayload = snapshot.CanonicalPayload.Replace(
                    "schemaVersion=1",
                    "schemaVersion=2",
                    StringComparison.Ordinal);
            return Task.FromResult(snapshot);
        }
    }

    private static ProductLegalEntityScopeInventorySnapshot ValidSnapshot(
        DateTimeOffset observedAtUtc,
        string canonicalPayload)
    {
        var stablePayload = string.Join(
            '\n',
            canonicalPayload.Split('\n', StringSplitOptions.None)[..^1]
                .Where(line => !line.StartsWith("observedUtcTicks=", StringComparison.Ordinal))) + "\n";
        return new ProductLegalEntityScopeInventorySnapshot
        {
            ObservedAtUtc = observedAtUtc,
            CanonicalPayload = canonicalPayload,
            StableFactsHash = Sha(stablePayload),
            SnapshotHash = Sha(canonicalPayload)
        };
    }

    private static string Sha(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.MdmService.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ThrowingScopeFactory(Action onCreate) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            onCreate();
            throw new InvalidOperationException("Scope must not be created.");
        }
    }
}
