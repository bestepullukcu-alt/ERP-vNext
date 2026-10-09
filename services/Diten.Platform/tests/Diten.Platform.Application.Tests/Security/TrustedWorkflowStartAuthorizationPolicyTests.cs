using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Workflow.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Security;

public sealed class TrustedWorkflowStartAuthorizationPolicyTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TemplateId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Empty_configuration_is_valid_and_denies_every_start()
    {
        var options = new TrustedWorkflowStartAuthorizationOptions();
        Assert.True(new TrustedWorkflowStartAuthorizationOptionsValidator()
            .Validate(null, options).Succeeded);

        var policy = new ConfiguredTrustedWorkflowStartAuthorizationPolicy(Options.Create(options));
        Assert.False(policy.IsAuthorized(Request()));
    }

    [Fact]
    public void Exact_template_id_tuple_is_authorized()
    {
        var policy = Policy(Entry());
        Assert.True(policy.IsAuthorized(Request()));
    }

    [Fact]
    public void Exact_template_code_tuple_is_authorized()
    {
        var policy = Policy(Entry(templateCode: "GP-APPROVAL"));
        Assert.True(policy.IsAuthorized(Request(templateCode: "GP-APPROVAL")));
    }

    [Fact]
    public void Any_tuple_drift_is_denied_without_normalization()
    {
        var policy = Policy(Entry());
        var exact = Request();

        Assert.False(policy.IsAuthorized(exact with { ClientId = Guid.NewGuid() }));
        Assert.False(policy.IsAuthorized(exact with { ServiceName = "diten.mdm" }));
        Assert.False(policy.IsAuthorized(exact with { Audience = "trusted_workflow_consumer" }));
        Assert.False(policy.IsAuthorized(exact with { ObjectType = "globalproduct" }));
        Assert.False(policy.IsAuthorized(exact with { TemplateId = Guid.NewGuid() }));
    }

    [Fact]
    public void Template_code_value_and_casing_drift_are_denied()
    {
        var policy = Policy(Entry(templateCode: "GP-APPROVAL"));

        Assert.False(policy.IsAuthorized(Request(templateCode: "gp-approval")));
        Assert.False(policy.IsAuthorized(Request(templateCode: "GP-RETIREMENT")));
    }

    [Fact]
    public void Template_selector_kind_must_match_the_authorized_tuple()
    {
        var idPolicy = Policy(Entry());
        var codePolicy = Policy(Entry(templateCode: "GP-APPROVAL"));

        Assert.False(idPolicy.IsAuthorized(Request(templateCode: "GP-APPROVAL")));
        Assert.False(codePolicy.IsAuthorized(Request()));
    }

    [Fact]
    public void Request_with_both_or_no_template_selector_is_denied()
    {
        var policy = Policy(Entry());

        Assert.False(policy.IsAuthorized(Request() with { TemplateCode = "GP-APPROVAL" }));
        Assert.False(policy.IsAuthorized(Request() with { TemplateId = null, TemplateCode = null }));
    }

    [Fact]
    public void Fields_cannot_be_mixed_across_two_authorized_tuples()
    {
        var secondTemplateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var policy = new ConfiguredTrustedWorkflowStartAuthorizationPolicy(Options.Create(OptionsWith(
            Entry(objectType: "GlobalProduct", templateId: TemplateId),
            Entry(objectType: "FinishedGood", templateId: secondTemplateId))));

        Assert.False(policy.IsAuthorized(Request(templateId: secondTemplateId)));
        Assert.False(policy.IsAuthorized(Request(templateId: TemplateId) with { ObjectType = "FinishedGood" }));
    }

    [Fact]
    public void Malformed_duplicate_wildcard_and_over_budget_configuration_fail_closed()
    {
        AssertInvalid(Entry(serviceName: "Diten.Other"));
        AssertInvalid(Entry(objectType: "Global*"));
        AssertInvalid(new TrustedWorkflowStartAuthorizationEntry
        {
            ClientId = ClientId,
            ServiceName = "Diten.MDM",
            Audience = "TRUSTED_WORKFLOW_CONSUMER",
            ObjectType = "GlobalProduct"
        });
        AssertInvalid(new TrustedWorkflowStartAuthorizationEntry
        {
            ClientId = ClientId,
            ServiceName = "Diten.MDM",
            Audience = "TRUSTED_WORKFLOW_CONSUMER",
            ObjectType = "GlobalProduct",
            TemplateId = TemplateId,
            TemplateCode = "GP-APPROVAL"
        });

        var duplicate = OptionsWith(Entry(), Entry());
        Assert.False(new TrustedWorkflowStartAuthorizationOptionsValidator().Validate(null, duplicate).Succeeded);

        var overBudget = OptionsWith(Enumerable.Range(0, 65)
            .Select(_ => Entry(clientId: Guid.NewGuid())).ToArray());
        Assert.False(new TrustedWorkflowStartAuthorizationOptionsValidator().Validate(null, overBudget).Succeeded);
    }

    private static void AssertInvalid(TrustedWorkflowStartAuthorizationEntry entry)
    {
        var result = new TrustedWorkflowStartAuthorizationOptionsValidator()
            .Validate(null, OptionsWith(entry));
        Assert.False(result.Succeeded);
        Assert.Contains(
            ConfiguredTrustedWorkflowStartAuthorizationPolicy.ConfigurationError,
            result.Failures ?? []);
    }

    private static ConfiguredTrustedWorkflowStartAuthorizationPolicy Policy(
        TrustedWorkflowStartAuthorizationEntry entry) => new(Options.Create(OptionsWith(entry)));

    private static TrustedWorkflowStartAuthorizationOptions OptionsWith(
        params TrustedWorkflowStartAuthorizationEntry[] entries) => new() { Entries = entries.ToList() };

    private static TrustedWorkflowStartAuthorizationEntry Entry(
        Guid? clientId = null,
        string serviceName = "Diten.MDM",
        string objectType = "GlobalProduct",
        Guid? templateId = null,
        string? templateCode = null) => new()
        {
            ClientId = clientId ?? ClientId,
            ServiceName = serviceName,
            Audience = "TRUSTED_WORKFLOW_CONSUMER",
            ObjectType = objectType,
            TemplateId = templateCode is null ? templateId ?? TemplateId : null,
            TemplateCode = templateCode
        };

    private static TrustedWorkflowStartAuthorizationRequest Request(
        Guid? templateId = null,
        string? templateCode = null) => new(
            ClientId,
            "Diten.MDM",
            "TRUSTED_WORKFLOW_CONSUMER",
            "GlobalProduct",
            templateCode is null ? templateId ?? TemplateId : null,
            templateCode);
}
