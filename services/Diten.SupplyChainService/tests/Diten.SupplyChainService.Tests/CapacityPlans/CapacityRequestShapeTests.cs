using System.Text.Json;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;

/// <summary>
/// Published SANDOP-CAPACITY 3.0.0 declares <c>additionalProperties: false</c> on all six request schemas
/// (measured in <c>docs/records/audits/2026-10/mvp6-sandop-capacity-conformance-02/</c>). The three S&amp;OP
/// operations enforce it through <c>SandopWire.Validate</c>'s allow-list; for Capacity it is the serializer's job,
/// and it is enforced twice over — by <c>JsonUnmappedMemberHandling.Disallow</c> on each of the three request
/// records, and again service-wide at <c>Program.cs:120</c>.
///
/// This guard holds the RECORDS to it. It deliberately does NOT set <c>UnmappedMemberHandling</c> in its own
/// options, because a guard that configures the behaviour it is checking measures its own copy rather than the
/// production type: the refusals below can only come from the attributes on the records themselves. Naming policy
/// and case sensitivity do match the service (<c>Program.cs:117-119</c>: web defaults, case-sensitive).
///
/// Each positive half has a negative half so the guard cannot pass by accident — a declared member must still bind.
/// </summary>
public sealed class CapacityRequestShapeTests
{
    /// <summary>Web defaults give the camelCase policy the service uses; case sensitivity matches Program.cs:119.
    /// UnmappedMemberHandling is left at its default ON PURPOSE — see the class comment.</summary>
    private static readonly JsonSerializerOptions ServiceLike =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false };

    private const string PlanBody =
        """{"name":"FY2027","horizonStart":"2027-01-01","horizonEnd":"2027-12-31","demandPlanId":"dp-2027","demandPlanVersion":"3","sourceCapturedAt":"2026-09-15T10:00:00Z","sourceChecksum":"abc123"}""";

    private const string ScenarioBody = """{"name":"Base","constraintRefs":[],"adjustments":[]}""";

    private const string EvaluateBody = """{"evaluationMode":"Finite","resourceRefs":["r1"]}""";

    private static T Bind<T>(string json) => JsonSerializer.Deserialize<T>(json, ServiceLike)!;

    private static string WithUnknown(string json) =>
        json.TrimEnd().TrimEnd('}') + ",\"notAContractMember\":\"x\"}";

    [Fact]
    public void An_unknown_member_is_refused_on_createCapacityPlan() =>
        Assert.Throws<JsonException>(() => Bind<CreateCapacityPlanRequest>(WithUnknown(PlanBody)));

    [Fact]
    public void An_unknown_member_is_refused_on_createCapacityScenario() =>
        Assert.Throws<JsonException>(() => Bind<CreateCapacityScenarioRequest>(WithUnknown(ScenarioBody)));

    [Fact]
    public void An_unknown_member_is_refused_on_evaluateCapacityScenario() =>
        Assert.Throws<JsonException>(() => Bind<EvaluateCapacityScenarioRequest>(WithUnknown(EvaluateBody)));

    [Fact]
    public void The_declared_members_still_bind_so_the_refusals_are_not_blanket()
    {
        var plan = Bind<CreateCapacityPlanRequest>(PlanBody);
        Assert.Equal("FY2027", plan.Name);
        Assert.Equal("dp-2027", plan.DemandPlanId);
        Assert.Equal("abc123", plan.SourceChecksum);
        Assert.Equal(new DateOnly(2027, 1, 1), plan.HorizonStart);

        var scenario = Bind<CreateCapacityScenarioRequest>(ScenarioBody);
        Assert.Equal("Base", scenario.Name);
        Assert.NotNull(scenario.ConstraintRefs);

        var evaluate = Bind<EvaluateCapacityScenarioRequest>(EvaluateBody);
        Assert.Equal("Finite", evaluate.EvaluationMode);
        Assert.Equal(["r1"], evaluate.ResourceRefs);
    }

    /// <summary>
    /// The sabotage half, without editing production code: <c>CapacityProvenance</c> is a record in the same file
    /// that does NOT carry the attribute, and under these same options an unknown member on it is SKIPPED. So the
    /// three refusals above are produced by the attributes on the request records, not by the serializer defaults
    /// or by this test's options — remove an attribute and its test goes red.
    /// </summary>
    [Fact]
    public void A_record_without_the_attribute_skips_the_unknown_member_so_the_refusals_above_are_attribute_driven()
    {
        const string provenance =
            """{"demandPlanId":"dp-2027","demandPlanVersion":"3","sourceContract":"DEMAND","sourceContractVersion":"v1","sourceCapturedAt":"2026-09-15T10:00:00Z","sourceChecksum":"abc123"}""";

        var control = Bind<CapacityProvenance>(WithUnknown(provenance));

        Assert.Equal("dp-2027", control.DemandPlanId);
        Assert.Equal("abc123", control.SourceChecksum);
    }

    [Fact]
    public void A_missing_required_member_is_refused()
    {
        var withoutChecksum = PlanBody.Replace(",\"sourceChecksum\":\"abc123\"", "");
        Assert.DoesNotContain("sourceChecksum", withoutChecksum);
        Assert.Throws<JsonException>(() => Bind<CreateCapacityPlanRequest>(withoutChecksum));
    }
}
