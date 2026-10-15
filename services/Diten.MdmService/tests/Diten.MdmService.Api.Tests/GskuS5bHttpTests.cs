using System.Net;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S5b (D) over HTTP — the maker repeats the SUBMIT (the same operation key) while its withdrawal is under
/// way: refused by name, no new engine start, the withdrawal left where it stands (the LSKU's Item02 test, for the GSKU).
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    [Fact]
    public async Task S5b_A_repeat_of_the_submit_during_its_withdrawal_is_refused_and_changes_nothing()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var submitKey = Guid.NewGuid();
        var submitted = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version },
            MakerToken(), submitKey);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var pending = await DetailAsync(gsku);
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        Assert.False((await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw",
            new { expectedGskuVersion = pending.Version, reasonCode = "MAKER_CORRECTION" }, MakerToken())).IsSuccessStatusCode);
        var operations = host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>("mdm_first_gsku_identity_workflow_operations");
        var before = await operations.Find(x => x.OperationId == submitKey).SingleAsync();
        Assert.True(FirstGskuIdentityWorkflowProcessor.InWithdrawal(before), before.Checkpoint.ToString());
        var starts = host.Engine.StartCalls;

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version },
            MakerToken(), submitKey);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowProcessor.WithdrawalUnderWay, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.Equal(starts, host.Engine.StartCalls);
        var after = await operations.Find(x => x.OperationId == submitKey).SingleAsync();
        Assert.Equal((before.Checkpoint, before.WithdrawalCommandId, before.LeaseGeneration),
            (after.Checkpoint, after.WithdrawalCommandId, after.LeaseGeneration));
    }
}
