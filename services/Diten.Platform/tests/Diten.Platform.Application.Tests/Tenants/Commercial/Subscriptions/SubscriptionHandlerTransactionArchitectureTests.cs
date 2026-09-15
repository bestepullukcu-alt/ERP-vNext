using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Subscriptions;

public sealed class SubscriptionHandlerTransactionArchitectureTests
{
    public static TheoryData<Type, Type> Handlers => new()
    {
        { typeof(CreateTenantSubscriptionCommandHandler), typeof(CreateTenantSubscriptionCommand) },
        { typeof(AssignPlanToTenantCommandHandler), typeof(AssignPlanToTenantCommand) },
        { typeof(ActivateTenantSubscriptionCommandHandler), typeof(ActivateTenantSubscriptionCommand) },
        { typeof(CancelTenantSubscriptionCommandHandler), typeof(CancelTenantSubscriptionCommand) },
        { typeof(ExpireTenantSubscriptionCommandHandler), typeof(ExpireTenantSubscriptionCommand) },
        { typeof(ReactivateTenantSubscriptionCommandHandler), typeof(ReactivateTenantSubscriptionCommand) },
        { typeof(RenewTenantSubscriptionCommandHandler), typeof(RenewTenantSubscriptionCommand) },
        { typeof(SuspendTenantSubscriptionCommandHandler), typeof(SuspendTenantSubscriptionCommand) }
    };

    [Theory]
    [MemberData(nameof(Handlers))]
    public void ExactEightHandlers_HaveTransactionalExecutableCallGraph(Type handlerType, Type commandType)
    {
        Assert.Contains(handlerType.GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(TenantSubscriptionTransactionWriter));
        Assert.True(typeof(ITransactionOwnedAuditCommand).IsAssignableFrom(commandType));

        var calls = GetExecutableCalls(handlerType.GetMethod("Handle")!);
        Assert.Contains(calls, method => method.DeclaringType == typeof(TenantSubscriptionTransactionWriter) ||
                                         method.DeclaringType?.Name == "TenantSubscriptionCommandSupport");
        Assert.DoesNotContain(calls, method => method.DeclaringType?.FullName == "Diten.BuildingBlocks.Eventing.IEventBus" &&
                                               method.Name == "PublishAsync");
        Assert.DoesNotContain(calls, method =>
            method.DeclaringType == typeof(ITenantSubscriptionRepository) &&
            method.Name is "CreateAsync" or "UpdateAsync" &&
            method.GetParameters().All(parameter => parameter.ParameterType.Name != "IPlatformTransactionSession"));
        Assert.DoesNotContain(calls, method =>
            method.DeclaringType == typeof(ITenantRegistryRepository) && method.Name == "UpdateAsync" &&
            method.GetParameters().All(parameter => parameter.ParameterType.Name != "IPlatformTransactionSession"));
    }

    [Theory]
    [InlineData(typeof(AssignPlanToTenantCommandHandler))]
    [InlineData(typeof(ActivateTenantSubscriptionCommandHandler))]
    public void QuotaOwningHandlers_CallTransactionBoundQuotaInitialization(Type handlerType)
    {
        var methods = handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Concat(handlerType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
        var calls = methods.SelectMany(GetExecutableCalls).ToList();
        Assert.Contains(calls, method => method.DeclaringType?.Name == "IQuotaService" &&
                                         method.Name == "InitializeSubscriptionQuotasAsync");
    }

    // BL-394 — every IL opcode keyed by its encoded value (two-byte opcodes carry the 0xFE prefix in the high byte).
    private static readonly IReadOnlyDictionary<ushort, OpCode> OpCodesByValue = BuildOpCodeTable();

    /// <summary>
    /// Walks the method body instruction by instruction, honouring each opcode's operand length.
    ///
    /// <para>BL-394: the previous version scanned every byte for 0x28/0x6f. A byte inside an operand (a metadata
    /// token, a branch offset) could match, the scan then skipped four bytes from the wrong place and stepped over a
    /// real call. Whether that happened depended on token values, so adding members ANYWHERE in the assembly flipped
    /// the test red with the handler unchanged. A real walk never reads an operand byte as an opcode, and every
    /// call/callvirt operand it hands to ResolveMethod is a genuine method token — so a resolution failure is no
    /// longer swallowed.</para>
    /// </summary>
    private static IReadOnlyList<MethodBase> GetExecutableCalls(MethodInfo method)
    {
        var target = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? method;
        var body = target.GetMethodBody()?.GetILAsByteArray() ?? [];
        var calls = new List<MethodBase>();
        var index = 0;
        while (index < body.Length)
        {
            ushort code = body[index];
            var opcodeLength = 1;
            if (code == 0xFE)
            {
                code = (ushort)(0xFE00 | body[index + 1]);
                opcodeLength = 2;
            }

            var opcode = OpCodesByValue[code];
            var operandStart = index + opcodeLength;
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            {
                var token = BitConverter.ToInt32(body, operandStart);
                var resolved = target.Module.ResolveMethod(token, target.DeclaringType?.GetGenericArguments(), null);
                if (resolved is not null) calls.Add(resolved);
            }

            index = operandStart + OperandLength(opcode.OperandType, body, operandStart);
        }
        return calls;
    }

    private static int OperandLength(OperandType operandType, byte[] body, int operandStart) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        // switch: a uint32 target count, then that many int32 offsets.
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(body, operandStart),
        // tokens, int32, int32 branch targets, float32
        _ => 4
    };

    private static IReadOnlyDictionary<ushort, OpCode> BuildOpCodeTable()
    {
        var table = new Dictionary<ushort, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opcode = (OpCode)field.GetValue(null)!;
            table[(ushort)opcode.Value] = opcode;
        }
        return table;
    }
}
