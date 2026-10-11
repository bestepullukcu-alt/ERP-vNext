# Permission gate — the single item blocking MVP-6 development (2026-10-03)

Measured, not assumed: `.claude/settings.local.json` reads **allow 123 / deny 118 / ask 0**.

Six live `Edit(...)` deny entries freeze every remaining backend work package:

| gate | freezes |
|---|---|
| `Edit(services/**)` | Q236 (fix is K3-proven), Q220, Q247 |
| `Edit(execution/**)` | Q233, Q234 |
| `Edit(gateway/**)` | Q241, Q242 |

Twelve further entries for the same paths use `Write(...)` / `NotebookEdit(...)` forms, which are
**never matched** by a file-write permission check — they are inert. 60 of the 118 deny entries are
inert for this reason.

`settings.local.PROPOSED.json` moves the six live gates from `deny` to `ask`: writes become
possible, and every single one still stops for explicit owner confirmation. It also drops the inert
forms. Nothing in `allow` is changed.

## Regenerating these numbers (K7 — keep the command, not the number)

```
python3 -c "import json;p=json.load(open('.claude/settings.local.json'))['permissions'];print('allow',len(p.get('allow',[])),'deny',len(p.get('deny',[])),'ask',len(p.get('ask',[])))"
```

## Why this file is here and not in a scratchpad

CT delivered this proposal once before, into `/private/tmp`. The machine restarted at 00:30:18 on
2026-10-03, `/private/tmp` was emptied, and the proposal was destroyed along with the scratch
evidence of five accepted work packages. See ledger row Q248: scratch is not system-of-record.

Nothing here is committed.

---

## OWNER DECISION — 2026-10-03

**Chosen: the six gates only.** CT offered to widen the change to `Edit(frontend/**)` and
`Edit(docs/analysis/**)` in the same step; the owner declined. Those two stay in `deny`
deliberately, not by oversight.

Verified before install (all four checks passed):

| check | result |
|---|---|
| `allow` list untouched | 123 → 123 |
| entries moved `deny` → `ask` | exactly 6, all `Edit(...)` on services/execution/gateway |
| `Edit(frontend/**)` and `Edit(docs/analysis/**)` | still DENIED, as decided |
| broad `Edit(`/`Write(` entries in `allow` that could swallow an `ask` | 0 |

### What this does NOT unblock

- **Q242** (MOD-0183 UI end-to-end) needs `frontend/**`. Still frozen. A later owner decision.
- Any work requiring a contract change under `docs/analysis/contracts/`. Still frozen (K12 applies).

### Accepted risks (CT, on the record)

- **R1 approval fatigue:** `ask` prompts on every write. Clicking through turns `ask` into `allow`
  in practice. Not technically measurable; stated so it is not discovered later.
- **R2 single layer:** the PreToolUse guard hook CT wrote was destroyed with `/private/tmp` in the
  00:30:18 restart, so `ask` is the only layer. CT can regenerate the hook on request.
- **R3 recovery:** GIT-002 keeps the commit gate independent, so a bad write stays uncommitted and
  visible in `git status`, and Q246-R2's archives are a second copy. Recovery is cheap but not free
  in a tree already carrying 665 dirty entries.

### Separate finding, not part of this change

`allow` contains `Bash(rm -f services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/TempDumpTemplate.cs)`
— a pre-approved deletion of a single file. It contradicts the owner's standing no-`rm` rule. CT
recommends removing it; left untouched here because this change does not modify `allow`.
