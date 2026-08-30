namespace Diten.PpmService.Domain.Exceptions;

public sealed class OptimisticConcurrencyException(string message) : Exception(message);
