# Owner decision — MOD-0192 Capacity UI: scenario and evaluation IDs in the Details address (F-Q79-05 = A) — 2026-09-26

Recorded by the Q90 chat lane at 2026-09-26T19:47+03:00 on CT instruction (CT writes no files).
Decision given by the owner through the question tool, 2026-09-26 ~19:50 +03:00 (CT conversation).

**Finding:** F-Q79-05 (CT verdict Q79, `docs/records/audits/2026-09/mvp6-ct-verdict-q79-2026-09-26.md`); options in the Q88 README
(`docs/records/audits/2026-09/mvp6-capacity-ui-draft-01/README.md`, SHA-256 `7ab015fc910c5c294aa83d3241a01b9eabddea9950475814c1e2ec22b1e885fc`, F-Q79-05 a/b/c).

**Decision: option A.** The scenario ID and the evaluation ID are carried in the Capacity plan Details page address (route or query),
so a reload or a shared link reopens the same view.

**Bounds:** no new endpoint, field or permission; no browser storage; existing get-scenario and get-evaluation adapters only.
Needs (1) a pack text patch to MOD-0192 §23.4 (prepared by Q90, sign-off Q92, apply Q93) and (2) a later draft-code revision of the
Q88 overlay (Q88c, after Q93). Does not authorize: code outside a CT-dispatched lane, shared-seam edits, contract changes, commit, push.
