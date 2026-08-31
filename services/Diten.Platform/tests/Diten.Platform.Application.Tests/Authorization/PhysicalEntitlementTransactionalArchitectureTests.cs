using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PhysicalEntitlementTransactionalArchitectureTests
{
    private static readonly Type[] ExactCommands =
    [
        typeof(AddTenantModuleEntitlementCommand),
        typeof(EnableTenantModuleEntitlementCommand),
        typeof(DisableTenantModuleEntitlementCommand),
        typeof(UpdateTenantModuleEntitlementExpiryCommand),
        typeof(RemoveTenantManualModuleOverrideCommand)
    ];

    private static readonly Type[] ExactHandlers =
    [
        typeof(AddTenantModuleEntitlementCommandHandler),
        typeof(EnableTenantModuleEntitlementCommandHandler),
        typeof(DisableTenantModuleEntitlementCommandHandler),
        typeof(UpdateTenantModuleEntitlementExpiryCommandHandler),
        typeof(RemoveTenantManualModuleOverrideCommandHandler)
    ];

    [Fact]
    public void TransactionOwnedAuditMarker_IsAppliedToExactFivePhysicalCommands()
    {
        var markedCommands = typeof(AddTenantModuleEntitlementCommand).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(AddTenantModuleEntitlementCommand).Namespace)
            .Where(type => typeof(ITransactionOwnedAuditCommand).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExactCommands.OrderBy(type => type.FullName, StringComparer.Ordinal),
            markedCommands);
    }

    [Fact]
    public void AuditBehavior_SkipsTransactionOwnedCommandsBeforeBuildingAnAuditPlan()
    {
        var source = ReadApplicationSource("Contracts", "Behaviors", "AuditBehavior.cs");
        var markerGuard = source.IndexOf("request is ITransactionOwnedAuditCommand", StringComparison.Ordinal);
        var planBuild = source.IndexOf("BuildAuditPlan(request)", StringComparison.Ordinal);

        Assert.True(markerGuard >= 0, "AuditBehavior must explicitly recognize transaction-owned audit commands.");
        Assert.True(planBuild > markerGuard, "The transaction-owned guard must run before the normal audit plan.");
        Assert.Contains("return await next()", source[markerGuard..planBuild], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditBehavior_DoesNotAppendOutsideTransactionForMarkedPhysicalCommand()
    {
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var behavior = new AuditBehavior<EnableTenantModuleEntitlementCommand, Response<NoContent>>(
            audit.Object,
            new AuditBehaviorOptions(),
            NullLogger<AuditBehavior<EnableTenantModuleEntitlementCommand, Response<NoContent>>>.Instance);
        var nextCalled = false;

        var response = await behavior.Handle(
            new EnableTenantModuleEntitlementCommand(Guid.NewGuid(), Guid.NewGuid(), null),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Response<NoContent>.Success(204));
            },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(response.IsSuccessful);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public void ExactFiveHandlers_UseOneTransactionAndTransactionalParticipantsWithoutDirectEventBus()
    {
        foreach (var handler in ExactHandlers)
        {
            var source = ReadApplicationSource(
                "Features", "Tenants", "Commercial", "Entitlements", "Handlers", "CommandHandlers",
                $"{handler.Name}.cs");

            Assert.Contains("IPlatformTransactionExecutor", source, StringComparison.Ordinal);
            Assert.Contains("_transactions.ExecuteAsync", source, StringComparison.Ordinal);
            Assert.Contains("IEntitlementStateVersionRepository", source, StringComparison.Ordinal);
            Assert.Contains("IncrementPhysicalEntitlementVersionAsync(session", source, StringComparison.Ordinal);
            Assert.Contains("ITransactionalIntegrationEventWriter", source, StringComparison.Ordinal);
            Assert.Contains("_events.EnqueueAsync(", source, StringComparison.Ordinal);
            Assert.Contains("ITransactionalAuditOutboxWriter", source, StringComparison.Ordinal);
            Assert.Contains("PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session", source, StringComparison.Ordinal);

            Assert.DoesNotContain("IEventBus", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PublishAsync(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExactFiveHandlers_KeepBusinessCounterEventAndAuditInsideTransactionBody()
    {
        foreach (var handler in ExactHandlers)
        {
            var source = ReadApplicationSource(
                "Features", "Tenants", "Commercial", "Entitlements", "Handlers", "CommandHandlers",
                $"{handler.Name}.cs");
            var transactionStart = source.IndexOf("_transactions.ExecuteAsync", StringComparison.Ordinal);
            var transactionEnd = source.IndexOf("}, ct)", transactionStart, StringComparison.Ordinal);

            Assert.True(transactionStart >= 0, $"{handler.Name} has no explicit transaction body.");
            Assert.True(transactionEnd > transactionStart, $"{handler.Name} transaction body boundary was not found.");
            var transactionBody = source[transactionStart..transactionEnd];

            Assert.Contains("session", transactionBody, StringComparison.Ordinal);
            Assert.Contains("IncrementPhysicalEntitlementVersionAsync(session", transactionBody, StringComparison.Ordinal);
            Assert.Contains("Enqueue", transactionBody, StringComparison.Ordinal);
            Assert.Contains("PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session", transactionBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryStateChangingPhysicalBranch_HasCounterEventAndAuditParticipants()
    {
        var expectedTransactionalBranches = new Dictionary<Type, int>
        {
            [typeof(AddTenantModuleEntitlementCommandHandler)] = 1,
            [typeof(EnableTenantModuleEntitlementCommandHandler)] = 1,
            [typeof(DisableTenantModuleEntitlementCommandHandler)] = 3,
            [typeof(UpdateTenantModuleEntitlementExpiryCommandHandler)] = 1,
            [typeof(RemoveTenantManualModuleOverrideCommandHandler)] = 1
        };

        foreach (var (handler, expectedBranches) in expectedTransactionalBranches)
        {
            var source = ReadApplicationSource(
                "Features", "Tenants", "Commercial", "Entitlements", "Handlers", "CommandHandlers",
                $"{handler.Name}.cs");

            Assert.Equal(expectedBranches, Count(source, "_transactions.ExecuteAsync"));
            Assert.Equal(expectedBranches, Count(source, "IncrementPhysicalEntitlementVersionAsync(session"));
            var branchEventToken = handler == typeof(DisableTenantModuleEntitlementCommandHandler)
                ? "EnqueueDisabledAsync(session"
                : "_events.EnqueueAsync(";
            Assert.Equal(expectedBranches, Count(source, branchEventToken));
            Assert.Equal(expectedBranches, Count(source, "PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session"));
        }
    }

    [Fact]
    public void ExactHandlers_PinEntitlementAndApplicableQuotaParticipants()
    {
        var expectations = new Dictionary<Type, (string Entitlement, string? Quota)>
        {
            [typeof(AddTenantModuleEntitlementCommandHandler)] = ("_repository.CreateAsync(session", "TryConsumeEntitlementAsync(session"),
            [typeof(EnableTenantModuleEntitlementCommandHandler)] = ("_repository.UpdateAsync(session", "TryConsumeEntitlementAsync(session"),
            [typeof(DisableTenantModuleEntitlementCommandHandler)] = ("_repository.UpdateAsync(session", "ReleaseEntitlementAsync(session"),
            [typeof(UpdateTenantModuleEntitlementExpiryCommandHandler)] = ("_repository.UpdateAsync(session", null),
            [typeof(RemoveTenantManualModuleOverrideCommandHandler)] = ("_repository.SoftDeleteAsync(session", "ReleaseEntitlementAsync(session")
        };

        foreach (var (handler, expected) in expectations)
        {
            var source = ReadApplicationSource("Features", "Tenants", "Commercial", "Entitlements", "Handlers", "CommandHandlers", $"{handler.Name}.cs");
            Assert.Contains(expected.Entitlement, source, StringComparison.Ordinal);
            if (expected.Quota is not null) Assert.Contains(expected.Quota, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TransactionQuotaTenantAndHarnessGuards_ArePinned()
    {
        var quota = ReadApplicationSource("Features", "Quotas", "Services", "QuotaService.cs");
        Assert.Contains("CountEnabledAsync(session", quota, StringComparison.Ordinal);
        Assert.DoesNotContain("CountEnabledAsync(request.TenantId", quota, StringComparison.Ordinal);

        var repository = ReadRepoFile("src", "Diten.Platform.Infrastructure", "Persistence", "Repositories", "TenantModuleEntitlementRepository.cs");
        Assert.Contains("Eq(x => x.TenantId, TenantContext.TenantId)", repository, StringComparison.Ordinal);
        Assert.Contains("PlatformMongoTransactionSession.Require(session, _dbContext)", repository, StringComparison.Ordinal);

        var executor = ReadRepoFile("src", "Diten.Platform.Infrastructure", "Persistence", "PlatformTransactionExecutor.cs");
        Assert.Contains("catch (OperationCanceledException)", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationCanceledException exception", executor, StringComparison.Ordinal);
        Assert.Contains("UnknownTransactionCommitResult", executor, StringComparison.Ordinal);

        var harness = ReadTestFile("Persistence", "DisposableMongoReplicaSet.cs");
        Assert.Contains("new TcpListener(IPAddress.Loopback, 0)", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("Port = 27017", harness, StringComparison.Ordinal);
        Assert.DoesNotContain("--port\", \"27017", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactFiveProductionHandlers_ILGraphCallsOnlySessionAwareAuthoritativeMutationSeams()
    {
        foreach (var handler in ExactHandlers)
        {
            var calls = handler.Assembly.GetTypes()
                .Where(type => type == handler || type.FullName?.Contains(handler.Name, StringComparison.Ordinal) == true)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .SelectMany(CalledMethods)
                .Where(method => method.DeclaringType == typeof(ITenantModuleEntitlementRepository))
                .Where(method => method.Name is "CreateAsync" or "UpdateAsync" or "SoftDeleteAsync")
                .ToArray();

            Assert.True(calls.Length > 0, $"{handler.Name} has no executable repository mutation edge.");
            Assert.All(calls, method => Assert.Equal(
                typeof(IPlatformTransactionSession), method.GetParameters()[0].ParameterType));
            Assert.DoesNotContain(calls, method => method.GetCustomAttribute<ObsoleteAttribute>() is not null);
        }
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var offset = 0; (offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length)
        {
            count++;
        }

        return count;
    }

    private static IEnumerable<MethodBase> CalledMethods(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        for (var offset = 0; offset < il.Length;)
        {
            var first = il[offset++];
            var opcode = first == 0xFE ? MultiByteOpCodes[il[offset++]] : SingleByteOpCodes[first];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, offset);
                MethodBase? called = null;
                try { called = method.Module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments()); }
                catch (ArgumentException) { }
                if (called is not null) yield return called;
            }
            offset += OperandSize(opcode.OperandType, il, offset);
        }
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildOpCodes(true);
    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodes(false);

    private static OpCode[] BuildOpCodes(bool singleByte)
    {
        var result = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opcode = (OpCode)field.GetValue(null)!;
            var value = unchecked((ushort)opcode.Value);
            if (singleByte == (value < 0x100)) result[value & 0xff] = opcode;
        }
        return result;
    }

    private static int OperandSize(OperandType type, byte[] il, int offset) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod
            or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
            or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, offset) * 4),
        _ => throw new NotSupportedException(type.ToString())
    };

    private static string ReadApplicationSource(params string[] segments)
    {
        var pathSegments = new[] { GetRepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application" }
            .Concat(segments)
            .ToArray();
        return File.ReadAllText(Path.Combine(pathSegments));
    }

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetRepoRoot(), "services", "Diten.Platform" }.Concat(segments).ToArray()));

    private static string ReadTestFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetRepoRoot(), "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests" }.Concat(segments).ToArray()));

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repo root could not be located.");
    }
}
