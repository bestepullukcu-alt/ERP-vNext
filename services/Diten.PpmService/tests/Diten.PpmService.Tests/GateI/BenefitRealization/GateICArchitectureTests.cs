using System.Reflection;
using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Xunit;

namespace Diten.PpmService.Tests.GateI.BenefitRealization;

public sealed class GateICArchitectureTests
{
    [Fact]
    public void Gate_ic_contracts_are_pure_domain_application_and_have_no_runtime_dependencies()
    {
        var assemblies = new[] { typeof(OutcomeReferenceV1).Assembly, typeof(BenefitCommitmentOutcomeReferenceValidator).Assembly };
        var forbidden = new[] { "MongoDB", "MassTransit", "AspNetCore", "RabbitMQ", "Ocelot" };
        foreach (var assembly in assemblies)
        foreach (var reference in assembly.GetReferencedAssemblies())
            Assert.DoesNotContain(forbidden, token => reference.Name?.Contains(token, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Validation_port_is_read_only_and_exposes_no_receipt_cache_or_mutation_method()
    {
        var methods = typeof(IOutcomeReferenceAuthorityPort).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var method = Assert.Single(methods);
        Assert.Equal("ValidateAsync", method.Name);
        var forbidden = new[] { "Attach", "Detach", "Retire", "Save", "Receipt", "Cache", "Audit", "Outbox" };
        Assert.DoesNotContain(forbidden, value => method.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contract_has_no_measurement_actual_evidence_or_realization_members()
    {
        var names = typeof(OutcomeReferenceV1).GetProperties().Select(x => x.Name)
            .Concat(typeof(BenefitCommitmentOutcomeReferenceV1).GetProperties().Select(x => x.Name)).ToArray();
        var forbidden = new[] { "Measurement", "Actual", "Period", "Evidence", "Realization", "IsReferenceable" };
        Assert.DoesNotContain(names, name => forbidden.Any(value => name.Contains(value, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Exact_operation_permission_and_identity_constants_are_closed()
    {
        Assert.Equal("outcome-tracking.outcome-references.validate", BenefitCommitmentOutcomeReferenceValidator.Operation);
        Assert.Equal("decision-intelligence.outcome-references.validate", BenefitCommitmentOutcomeReferenceValidator.Permission);
        Assert.Equal("diten-decision-intelligence-service", BenefitCommitmentOutcomeReferenceValidator.Audience);
        Assert.Equal("diten.decision-intelligence", BenefitCommitmentOutcomeReferenceValidator.ClientId);
        Assert.Equal("MOD-0072", BenefitCommitmentOutcomeReferenceValidator.OwnerModule);
        Assert.Equal("diten.s2s.delegated.invoke", BenefitCommitmentOutcomeReferenceValidator.Scope);
    }
}
