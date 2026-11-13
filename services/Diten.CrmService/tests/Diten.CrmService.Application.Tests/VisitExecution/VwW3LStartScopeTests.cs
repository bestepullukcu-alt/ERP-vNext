using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitWorkspace;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3L · W3-BE-d (K-L1 / K-L5) on the PRODUCTION start handler: the start runs under the rep's LAYERED rules (a legal
/// entity layer that makes the location informative accepts a far start the tenant would refuse) and keeps the scope chain
/// it was resolved through; a failing Platform skips the layer, says so on the record and never blocks the start.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private static readonly Guid StartLe = Guid.Parse("0e000000-0000-0000-0000-0000000000a1");

    private StartVisitExecutionHandler LayeredStarter(FakeLayerRepository layers, FakeSeatReader seats)
    {
        var chains = new EvidenceScopeChainResolver(new EvidenceBusinessUnitReader(new FakeTerritoryModelRepo(), new FakeTerritoryResourceAssignmentRepo()), seats);
        return new(TenantCtx(), new Application.Common.NullActorContext(), RepCaller(), _plans, _reports, _executions, _policies,
            _accounts, _store, _clock, new VisitEvidencePolicyResolver(_policies, null, layers, chains));
    }

    [Fact]
    public async Task W3L_the_start_runs_under_the_legal_entity_layer_and_keeps_the_chain_and_a_failing_platform_never_blocks_it()
    {
        var layers = new FakeLayerRepository();
        layers.Add(Tenant, VisitEvidencePolicyScopeTypes.LegalEntity, StartLe.ToString("D"), l => l.LocationMode = VisitLocationModes.Info);
        var seats = new FakeSeatReader();
        seats.Seat(Rep, StartLe);

        // the tenant (location required) would refuse this far start; the rep's legal entity makes the location informative
        var started = await LayeredStarter(layers, seats).Handle(StartAt(Seed(), lat: FarLat), default);
        Assert.Equal(201, started.StatusCode);
        Assert.Equal(VisitLocationModes.Info, started.Data!.Location.Mode);
        var scope = _executions.Items.Last().EvidenceScope!;
        Assert.Equal((StartLe, (string?)null), (scope.LegalEntityId, scope.BusinessUnit));
        Assert.Equal(("resolved", "no-territory", "no-territory"), (scope.LegalEntityNote!, scope.BusinessUnitNote!, scope.Note));
        Assert.Null(_policies.Stored);

        // the Platform down: the legal entity layer is skipped (the tenant's "required" applies), the record says why
        seats.Fail = true;
        var refused = await LayeredStarter(layers, seats).Handle(StartAt(Seed(), lat: FarLat), default);
        AssertCode(refused, 409, VisitExecutionErrorCodes.LocationTooFar);
        var near = await LayeredStarter(layers, seats).Handle(StartAt(Seed(), lat: NearLat), default);
        Assert.Equal(201, near.StatusCode);
        var down = _executions.Items.Last().EvidenceScope!;
        Assert.Null(down.LegalEntityId);
        Assert.Equal(EvidenceScopeNotes.PlatformUnavailable, down.LegalEntityNote);
        Assert.StartsWith(EvidenceScopeNotes.PlatformUnavailable, down.Note);
    }
}
