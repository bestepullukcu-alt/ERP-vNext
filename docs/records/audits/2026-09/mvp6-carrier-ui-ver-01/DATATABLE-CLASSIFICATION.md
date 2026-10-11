# DataTable verifier classification

Fresh result: **82 PASS / 9 FAIL**, process exit 1.

| Failure | Classification | Verdict |
|---|---|---|
| tenant header in shared `personalization-client.js` | Existing protected shared-surface gap; unchanged from HEAD (`e80cd857...`) and outside the 21 UI-owned paths | Real open gap; DataTable gate PARTIAL |
| direct-Gateway profile requires `window.API.*` | False positive for this pack; the approved profile is same-origin MVC proxy | Not a Carrier defect |
| select-all checkbox | Pack forbids bulk/delete | Required absence |
| bulk action config | Pack forbids bulk/delete | Required absence |
| bulk selection wiring | Pack forbids bulk/delete | Required absence |
| `/bulk` endpoint | Pack forbids new backend/bulk endpoint | Required absence |
| bulk delete trigger | Pack forbids delete/bulk UI | Required absence |
| delete-oriented `reloadWithToast` lifecycle | No single-row or bulk delete exists | Inapplicable verifier assumption |
| clear-selection wiring | No bulk selection exists | Required absence |

The verifier itself cannot express the pack's bounded list/create/status profile, so its raw nonzero result is
retained. The protected personalization failure prevents promotion of this gate to PASS.

