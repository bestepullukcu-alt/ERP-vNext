using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public sealed class DecisionTraceContractException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);
internal static class DecisionTraceGuard { public static Guid Id(Guid value, string name) => value == Guid.Empty ? throw new DecisionTraceContractException($"{name} cannot be empty.") : value; }
