using System.Reflection;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

public sealed class SubscriptionTransactionFailureArchitectureTests
{
    [Fact]
    public void ProductionComposition_IsPlatformExecutorSingleContextAndDefaultNoOpProbe()
    {
        var writerConstructor = typeof(TenantSubscriptionTransactionWriter).GetConstructors().Single();
        Assert.Single(writerConstructor.GetParameters(), parameter => parameter.ParameterType == typeof(IPlatformTransactionExecutor));

        var executorConstructor = typeof(PlatformTransactionExecutor).GetConstructors().Single();
        Assert.Equal(new[] { typeof(IPlatformDbContext), typeof(IPlatformTransactionFaultProbe) },
            executorConstructor.GetParameters().Select(x => x.ParameterType));
        Assert.Single(executorConstructor.GetParameters(), x => x.ParameterType == typeof(IPlatformDbContext));
        Assert.DoesNotContain(executorConstructor.GetParameters(), x => x.ParameterType.FullName == "MongoDB.Driver.IMongoClient");

        var context = new Moq.Mock<IPlatformDbContext>();
        var executor = new PlatformTransactionExecutor(context.Object);
        var probe = typeof(PlatformTransactionExecutor).GetField("_faultProbe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(executor);
        Assert.IsType<NoOpPlatformTransactionFaultProbe>(probe);
    }

    [Fact]
    public void UnknownCommitPath_IsCommitProbeBoundAndSeparateFromBodyRetryClassifier()
    {
        var methods = typeof(PlatformTransactionExecutor).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
        Assert.Contains(methods, method => method.Name == "IsTransientBodyFailure");
        Assert.Equal(typeof(IPlatformTransactionFaultProbe),
            typeof(PlatformTransactionExecutor).GetField("_faultProbe", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(Task), typeof(IPlatformTransactionFaultProbe).GetMethod("BeforeCommitAsync")!.ReturnType);
        Assert.Equal(typeof(Task), typeof(IPlatformTransactionFaultProbe).GetMethod("AfterCommitAsync")!.ReturnType);
    }
}
