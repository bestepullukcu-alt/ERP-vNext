using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// R-2 (PR #134). The five *ContextMiddleware classes now take ILegalEntityScopeValidator. Tests that exercise
/// scope, header, correlation or permission behaviour pass <see cref="Valid"/> so their facts keep the meaning they
/// had before the validator existed; tests about the validation itself pass the outcome they are asserting.
/// Records every call, so a test can also prove the middleware asked at all — or did not.
/// </summary>
public sealed class StubLegalEntityScopeValidator(LegalEntityScopeOutcome outcome) : ILegalEntityScopeValidator
{
    public static StubLegalEntityScopeValidator Valid => new(LegalEntityScopeOutcome.Valid);

    /// <summary>
    /// H2: settable, so a host test can drive one request through the real pipeline with MDM answering "not
    /// referenceable" or being unreachable, and measure the refusal and that nothing was written. Reset it to
    /// Valid in a finally — the host is shared by its collection.
    /// </summary>
    public LegalEntityScopeOutcome Outcome { get; set; } = outcome;

    public List<(Guid TenantId, Guid LegalEntityId, string Authorization, Guid CorrelationId)> Calls { get; } = [];

    public Task<LegalEntityScopeOutcome> ValidateAsync(
        Guid tenantId,
        Guid legalEntityId,
        string authorization,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        lock (Calls) Calls.Add((tenantId, legalEntityId, authorization, correlationId));
        return Task.FromResult(Outcome);
    }
}
