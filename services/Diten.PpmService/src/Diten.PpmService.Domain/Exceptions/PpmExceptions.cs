namespace Diten.PpmService.Domain.Exceptions;

public sealed class OptimisticConcurrencyException(string message) : Exception(message);
public sealed class TransactionUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
