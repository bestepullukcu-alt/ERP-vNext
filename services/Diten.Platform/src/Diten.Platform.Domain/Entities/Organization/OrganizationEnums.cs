namespace Diten.Platform.Domain.Entities.Organization;

// MOD-0288 v1 — fixed, code-owned enums for the Organization module (localized on the frontend via resx, NOT
// reference-data). Order/values are a stable contract; append new members at the end.

// MOD-0288-FU03 — `GroupFunction` is APPENDED, never inserted. The enum is persisted by value, so moving an
// existing member rewrites the type of every stored unit silently.
public enum OrgUnitType { Department, Division, Branch, Team, HQ, GroupFunction }

public enum OrgUnitStatus { Active, Inactive }

public enum PositionType { Permanent, Temporary, Contractor, Intern }

public enum PositionStatus { Draft, Active, Frozen, Closed }

public enum AssignmentType { Primary, Secondary, Acting, Delegated }

public enum AssignmentReason { Hire, Transfer, Promotion, Backfill }

// Derived, never stored — computed from EffectiveFrom/To + IsCancelled relative to "now".
public enum AssignmentDerivedStatus { Planned, Active, Ended }
