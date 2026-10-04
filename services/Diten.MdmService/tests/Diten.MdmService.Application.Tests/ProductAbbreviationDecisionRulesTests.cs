using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-ABB-LIST-01 EK 9 — the shared "another approver decided" rule, both ways: who it says yes for, and every
/// state it must say no for (each of those keeps its old answer).
/// </summary>
public sealed class ProductAbbreviationDecisionRulesTests
{
    private const string Me = "11111111-1111-1111-1111-111111111111";
    private const string Other = "22222222-2222-2222-2222-222222222222";

    private static ProductAbbreviationRegisterEntry Entry(
        ProductAbbreviationLifecycleStatus status,
        string? decider,
        string? retirementRequestId = null) => new()
    {
        Id = Guid.NewGuid(),
        LifecycleStatus = status,
        LastDecisionByCanonicalSubjectId = decider,
        RetirementRequestId = retirementRequestId
    };

    [Theory]
    [InlineData(ProductAbbreviationLifecycleStatus.ACTIVE)]
    [InlineData(ProductAbbreviationLifecycleStatus.REJECTED)]
    public void Another_persons_approval_or_rejection_is_another_approvers_decision(ProductAbbreviationLifecycleStatus status)
        => Assert.True(ProductAbbreviationDecisionRules.DecidedByAnotherApprover(Entry(status, Other), Me));

    [Theory]
    [InlineData(ProductAbbreviationLifecycleStatus.ACTIVE)]
    [InlineData(ProductAbbreviationLifecycleStatus.REJECTED)]
    public void My_own_decision_under_another_key_is_not_another_approvers(ProductAbbreviationLifecycleStatus status)
        => Assert.False(ProductAbbreviationDecisionRules.DecidedByAnotherApprover(Entry(status, Me), Me));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_decision_with_no_recorded_decider_is_not_another_approvers(string? decider)
        => Assert.False(ProductAbbreviationDecisionRules.DecidedByAnotherApprover(
            Entry(ProductAbbreviationLifecycleStatus.REJECTED, decider), Me));

    [Theory]
    [InlineData(ProductAbbreviationLifecycleStatus.RETIRED)]
    [InlineData(ProductAbbreviationLifecycleStatus.CANCELLED)]
    [InlineData(ProductAbbreviationLifecycleStatus.REQUESTED)]
    public void Retired_cancelled_or_still_requested_is_not_another_approvers_decision(ProductAbbreviationLifecycleStatus status)
        => Assert.False(ProductAbbreviationDecisionRules.DecidedByAnotherApprover(Entry(status, Other), Me));

    [Fact]
    public void An_active_code_whose_retirement_waits_is_not_another_approvers_decision()
        => Assert.False(ProductAbbreviationDecisionRules.DecidedByAnotherApprover(
            Entry(ProductAbbreviationLifecycleStatus.ACTIVE, Other, retirementRequestId: "retire-1"), Me));
}
