using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


/// <summary>Consumes the authoritative, PSS-approved effective-permission result. It never computes roles or grants.</summary>
public interface IEffectivePermissionEvaluator
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken);
}
