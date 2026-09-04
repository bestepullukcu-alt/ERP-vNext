using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public enum DecisionTraceValidationMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }
