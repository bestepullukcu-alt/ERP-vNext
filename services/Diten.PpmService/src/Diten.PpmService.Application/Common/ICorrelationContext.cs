using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public interface ICorrelationContext { Guid CorrelationId { get; } }
