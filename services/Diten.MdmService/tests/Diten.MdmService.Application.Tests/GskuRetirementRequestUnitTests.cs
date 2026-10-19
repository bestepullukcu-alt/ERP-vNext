using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuRetirementRequestUnitTests
{
    [Fact]
    public void Validator_rejects_non_exact_request_facts()
    {
        var command = new StartGskuRetirementRequestWorkflowCommand(
            new(Guid.Empty, -1, Guid.Empty, " invalid "));
        var result = new StartGskuRetirementRequestWorkflowValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Options_are_disabled_and_reject_template_collisions()
    {
        var template = Guid.NewGuid();
        Assert.False(new GskuRetirementRequestWorkflowOptions().Enabled);
        Assert.Throws<InvalidOperationException>(() => new GskuRetirementRequestWorkflowOptions
        {
            Enabled = true, TemplateId = template, CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "GSKU_RETIREMENT"
        }.ToConfiguration(template, null, Guid.NewGuid(), null));
        Assert.False(new GskuRetirementRequestWorkflowWorkerOptions { BatchSize = 101 }.IsValid());
    }

    [Fact]
    public void Cli_accepts_only_exact_single_argument()
    {
        Assert.True(GskuRetirementRequestRecoveryCommandLine.IsRequested(
            [GskuRetirementRequestRecoveryCommandLine.ExactArgument]));
        Assert.False(GskuRetirementRequestRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() => GskuRetirementRequestRecoveryCommandLine.IsRequested(
            ["--run-gsku-retirement-request-recovery=true"]));
    }
}
