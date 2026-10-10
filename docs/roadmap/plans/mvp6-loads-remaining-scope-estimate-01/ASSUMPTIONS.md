# Assumptions and boundaries

1. One person-day remains eight person-hours, inherited from the Loads UI estimate; it is a planning convention, not measured duration.
2. The 48/84/144 first-slice estimate remains a replacement estimate for list/create UI work. No part of it is reused for transition UI.
3. `0185-2-REMAINING`, `0185-5-LIVE-REMAINING` and `0185-6-LIVE-REMAINING` are retained. Contract/root decision, transition integration and cross-module verification are mapped against them before adding hours.
4. Transition UI uses the existing published `transitionLoad` lifecycle, permissions, response statuses, error codes, correlation and replay policy. A policy change requires re-estimation.
5. Root access is mandatory but the access shape is not selected. The alternatives in `SCENARIOS.tsv` are planning branches, not endpoint/schema proposals.
6. A separate detail page and searchable lookup selectors need owner scope decisions. Zero and unestimated scenarios are shown instead of false precision.
7. The current Shipment Root R2 emission defect remains a separate unestimated record. Its future patch or verification is not forecast as completed and is not charged to Loads.
8. Percentages are estimated-effort scope indices based on most-likely hours. They are not readiness, elapsed effort, calendar progress or production readiness.
