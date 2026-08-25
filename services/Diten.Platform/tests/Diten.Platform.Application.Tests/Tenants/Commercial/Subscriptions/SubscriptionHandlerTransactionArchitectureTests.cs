using System.Reflection;
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

    private static IReadOnlyList<MethodBase> GetExecutableCalls(MethodInfo method)
    {
        var target = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? method;
        var body = target.GetMethodBody()?.GetILAsByteArray() ?? [];
        var calls = new List<MethodBase>();
        for (var index = 0; index < body.Length; index++)
        {
            var opcode = body[index];
            if (opcode is not (0x28 or 0x6f)) continue; // call / callvirt
            if (index + 4 >= body.Length) break;
            var token = BitConverter.ToInt32(body, index + 1);
            try
            {
                var resolved = target.Module.ResolveMethod(token, target.DeclaringType?.GetGenericArguments(), null);
                if (resolved is not null) calls.Add(resolved);
            }
            catch (ArgumentException) { }
            index += 4;
        }
        return calls;
    }
}
