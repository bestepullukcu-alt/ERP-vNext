using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuCorrectionUnitTests
{
    [Fact]
    public void Validator_rejects_unknown_invalid_or_non_exact_proposal_facts()
    {
        var invalid = new StartGskuCorrectionWorkflowCommand(new(Guid.Empty, -1, Guid.Empty, 0, " EA "));
        var result = new StartGskuCorrectionWorkflowValidator().Validate(invalid);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5);
    }

    [Fact]
    public void Options_are_default_disabled_and_fail_closed_on_template_collision()
    {
        var template = Guid.NewGuid();
        Assert.False(new GskuCorrectionWorkflowOptions().Enabled);
        Assert.Throws<InvalidOperationException>(() => new GskuCorrectionWorkflowOptions
        {
            Enabled = true, TemplateId = template, CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "GSKU_CORRECTION"
        }.ToConfiguration(template, null));
        Assert.False(new GskuCorrectionWorkflowWorkerOptions { BatchSize = 101 }.IsValid());
    }

    [Fact]
    public void Command_line_accepts_only_one_exact_case_sensitive_argument()
    {
        Assert.True(GskuCorrectionRecoveryCommandLine.IsRequested(
            [GskuCorrectionRecoveryCommandLine.ExactArgument]));
        Assert.False(GskuCorrectionRecoveryCommandLine.IsRequested([]));
        Assert.Throws<InvalidOperationException>(() => GskuCorrectionRecoveryCommandLine.IsRequested(
            [GskuCorrectionRecoveryCommandLine.ExactArgument, GskuCorrectionRecoveryCommandLine.ExactArgument]));
        Assert.Throws<InvalidOperationException>(() => GskuCorrectionRecoveryCommandLine.IsRequested(
            ["--run-gsku-correction-recovery=true"]));
    }
}
