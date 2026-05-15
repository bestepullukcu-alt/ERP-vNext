namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditRecursionGuard
{
    bool IsActive { get; }
    IDisposable BeginScope();
}
