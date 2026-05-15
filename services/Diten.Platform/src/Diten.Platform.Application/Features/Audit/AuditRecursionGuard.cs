using System.Threading;
using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.Application.Features.Audit;

public sealed class AuditRecursionGuard : IAuditRecursionGuard
{
    private readonly AsyncLocal<int> _depth = new();

    public bool IsActive => _depth.Value > 0;

    public IDisposable BeginScope()
    {
        _depth.Value++;
        return new Scope(this);
    }

    private void EndScope()
    {
        if (_depth.Value > 0)
        {
            _depth.Value--;
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly AuditRecursionGuard _guard;
        private bool _disposed;

        public Scope(AuditRecursionGuard guard)
        {
            _guard = guard;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _guard.EndScope();
            _disposed = true;
        }
    }
}
