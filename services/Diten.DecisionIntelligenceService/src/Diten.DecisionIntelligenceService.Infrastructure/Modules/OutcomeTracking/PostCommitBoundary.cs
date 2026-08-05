using Diten.BuildingBlocks.Eventing;
namespace Diten.DecisionIntelligenceService.Infrastructure.Modules.OutcomeTracking;
public sealed class PostCommitBoundary(IEventBus eventBus){public IEventBus EventBus{get;}=eventBus;}
