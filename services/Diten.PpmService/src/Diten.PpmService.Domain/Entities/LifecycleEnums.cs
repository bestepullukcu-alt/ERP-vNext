namespace Diten.PpmService.Domain.Entities;

public enum PortfolioLifecycleState { Draft, Active, Archived }
public enum InitiativeLifecycleState { Proposed, Active, OnHold, Completed, Cancelled }
public enum ProgramLifecycleState { Draft, Active, OnHold, Completed, Cancelled }
public enum ProjectLifecycleState { Draft, Planned, Active, OnHold, Completed, Cancelled }
public enum ProjectParentType { Initiative, Program }
