using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

internal sealed class PhysicalEntitlementMutationRejectedException(IReadOnlyList<string> errors, int statusCode)
    : Exception("Physical entitlement mutation was rejected before commit.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
    public int StatusCode { get; } = statusCode;
}
