using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX4 item 1 (b) / FIX5 item 6: a STARTED request carries the template id the engine resolved
/// from the configured code. A configuration that names only the code must still recognise the maker's repeat of that
/// request — else every repeat after a start is "another operation" (409) and a stuck request has no way out. One that
/// names the id still holds the request to it.
/// </summary>
public sealed class StartedRequestConfigurationMatchTests
{
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly Guid EngineTemplateId = Guid.NewGuid();

    [Theory]
    [InlineData(null, true)]
    [InlineData("engine", true)]
    [InlineData("other", false)]
    public void The_global_product_correction_recognises_its_started_request(string? configuredId, bool matches)
    {
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            Configured(configuredId), configuredId is null ? "GP-CORRECTION" : null, [Approver], "CORRECTION", false, false,
            null, null, "GP-IDENTITY"), TimeProvider.System);
        var started = new GlobalProductCorrectionOperation
        {
            WorkflowTemplateId = EngineTemplateId, WorkflowTemplateCode = configuredId is null ? "GP-CORRECTION" : null,
            CandidatePrincipalIds = [Approver], ReasonCode = "CORRECTION"
        };

        Assert.Equal(matches, factory.MatchesConfiguration(started));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("engine", true)]
    [InlineData("other", false)]
    public void The_gsku_correction_recognises_its_started_request(string? configuredId, bool matches)
    {
        var factory = new GskuCorrectionWorkflowStartRequestFactory(new(
            Configured(configuredId), configuredId is null ? "GSKU-CORRECTION" : null, [Approver], "GSKU_CORRECTION",
            false, false, null, null, "GSKU-IDENTITY"), TimeProvider.System);
        var started = new GskuCorrectionWorkflowOperation
        {
            WorkflowTemplateId = EngineTemplateId, WorkflowTemplateCode = configuredId is null ? "GSKU-CORRECTION" : null,
            CandidatePrincipalIds = [Approver], ReasonCode = "GSKU_CORRECTION"
        };

        Assert.Equal(matches, factory.MatchesConfiguration(started));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("engine", true)]
    [InlineData("other", false)]
    public void The_gsku_retirement_request_recognises_its_started_request(string? configuredId, bool matches)
    {
        var factory = new GskuRetirementRequestWorkflowStartRequestFactory(new(
            Configured(configuredId), configuredId is null ? "GSKU-RETIREMENT-REQUEST" : null, [Approver],
            "GSKU_RETIREMENT_REQUEST", false, false, null, null, "GSKU-IDENTITY", null, "GSKU-CORRECTION"),
            TimeProvider.System);
        var started = new GskuRetirementRequestOperation
        {
            WorkflowTemplateId = EngineTemplateId,
            WorkflowTemplateCode = configuredId is null ? "GSKU-RETIREMENT-REQUEST" : null,
            CandidatePrincipalIds = [Approver], ReasonCode = "GSKU_RETIREMENT_REQUEST"
        };

        Assert.Equal(matches, factory.MatchesConfiguration(started));
    }

    private static Guid? Configured(string? configuredId) => configuredId switch
    {
        null => null,
        "engine" => EngineTemplateId,
        _ => Guid.NewGuid()
    };
}
