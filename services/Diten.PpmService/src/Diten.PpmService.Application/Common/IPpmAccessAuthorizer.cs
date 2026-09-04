using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public interface IPpmAccessAuthorizer
{
    Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken cancellationToken);
}
