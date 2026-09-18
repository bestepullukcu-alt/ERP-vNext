using Diten.ProcurementService.Application.Interfaces;

namespace Diten.ProcurementService.Api.Tests.InvoiceMatch;

/// <summary>
/// Test double for ICurrentUserContext — supplies a deterministic actor so ResolveMatchExceptionHandler can write the
/// approval trail (resolvedBy) that MOD-0023/E4 requires. The production implementation reads it from JWT claims.
/// </summary>
public sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public FakeCurrentUserContext(string userName = "ap-approver", Guid? userId = null)
    {
        UserName = userName;
        UserId = userId ?? Guid.NewGuid();
    }

    public Guid UserId { get; }

    public string UserName { get; }
}
