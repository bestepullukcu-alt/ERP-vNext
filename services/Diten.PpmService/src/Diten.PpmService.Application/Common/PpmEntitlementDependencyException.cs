using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public sealed class PpmEntitlementDependencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
