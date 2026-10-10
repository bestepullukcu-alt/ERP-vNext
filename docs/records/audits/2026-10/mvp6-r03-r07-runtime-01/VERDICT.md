# Q264 — R-03 / R-05 / R-07 measured live · verdict

| Field | Value |
|---|---|
| WP / evidence | Q264 · E4 (live, started services) |
| Placement | Claude app → Code tab → Local, Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | `read-only-auditor` (measured) + `documentation-writer` (this folder). §17.4 has no row for either; fields by analogy, as dispatched |
| Branch / HEAD | `feature/mvp6-logistics` @ `4a8d4d4b3` |
| Window (Europe/Istanbul) | preflight 12:46:25 · stack up 12:49:42–12:56:48 · record written after shutdown |
| Identity | a project-seeded tenant user of tenant `00000000-0000-0000-0000-000000000001`, `roles: []`, no `supplychain.shipments.*` key. Seeded automatically by Auth into the fresh `diten_auth_q264` database at start; this WP created no user and granted nothing |

## Stack (`STACK.tsv`)

All five services started on their fixed ports in `Development` and stayed up. **No row was lost to a
partial stack.** Auth and Platform answered `/health` 503 for the whole run: their Development config uses
RabbitMQ, which is not running; `/health/live` was 200 and sign-in worked. Auth and Web were rebuilt first
because their binaries (24 Aug) predated their sources (2 Oct).

## R-03 — the denied screen: **MET**

| Page | Skeleton | Empty table | Action button | Redirect | Shell kept | Page status | XHRs |
|---|---|---|---|---|---|---|---|
| Index | no | no | no | no | yes | 200 | shell search only |
| Create | no | no | no | no | yes | 200 | shell search only |
| Details | no | no | no | no | yes | 200 | shell search only |

Each page renders exactly one card: lock icon, "You don't have access to …", "Ask your administrator to grant
you permission for this page." — inside the tenant shell, address bar unchanged. Saved HTML: `html/r03-*.html`.
UAS-001 §§1, 2, 3 (row 2: inside the shell), 5 and 6 hold on all three pages. One gap against the letter: the
page answers HTTP **200**, while UAS-001 §3 labels this case 403 (F-Q264-2).

## R-05 — UX states: **NOT MET** (not measurable beyond one state)

Only the **denied** state could be reached. It renders correctly at desktop width, at 375 × 812 (no horizontal
scroll) and in Arabic (**RTL exercised**: `dir=rtl`, `lang=ar`, Arabic text, mirrored shell). Loading, empty,
validation-error, save-failed and conflict need a user who holds the keys; none exists, and granting one is
reserved by pack §14. 15 view × state rows are recorded as NOT MEASURABLE (`UX-STATES.tsv`).

**D-1 and D-2 were neither reproduced nor falsified.** Both live behind `index.js`, which the denied page never
loads. The list XHR, sent from the same session, is answered **403 by the Web adapter itself**
(`SupplyChainShipmentsController.cs:57`) before any gateway call; with SupplyChain stopped the user sees the
identical denied card. D-1 needs a user with page-level read whose API call is denied; D-2 needs a user with
read whose list load fails. Neither identity is available without a grant.

## R-07 — correlation across three logs: **NOT MET**

| Log | UUID `800cad66-7372-494b-bc5a-f004b112082c` |
|---|---:|
| Web | 0 |
| Gateway | 0 |
| SupplyChain | 0 |

The only shipment request this user can cause stops at Web (403), and **Web does not log the correlation id
it issued**. Supplementary, outside the browser: one unauthenticated request straight to the gateway with a
known id — Gateway log **2**, SupplyChain log **0** although the service received it and echoed the id back,
Web 0 (not involved). So even on a full pass, two of the three logs would not carry the id today.

## What a partial stack prevented

Nothing: the stack was complete. What prevented R-05, D-1, D-2 and a full R-07 is the missing permission seed,
not the stack.

## Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q264-1** | 🟠 High — **sixth premise falsified** | The dispatch says D-1 can be produced directly because "the signed-in user has no permission, so the list API returns 403". It cannot: without the key the page never loads `index.js` and fires no list XHR. D-1 is only reachable by a user whom the page lets in and the API refuses. | R03-DENIED.tsv; `Index.cshtml:9-13` |
| **F-Q264-2** | 🟡 Medium | Denied pages answer HTTP 200. UAS-001 §3 assigns 403 to "session, no permission". | navigation `responseStatus` = 200 on all three |
| **F-Q264-3** | 🟠 High | Correlation is not logged end to end: Web log 0 for an id it put in its own response; SupplyChain log 0 for an id it received and echoed. Only the gateway logs it. | CORRELATION.tsv |
| **F-Q264-4** | 🟡 Medium | `PERSISTENCE_UNAVAILABLE` and `INTERNAL_ERROR` **are** sent — by the Web adapter on gateway failure/timeout (`SupplyChainShipmentsController.cs:134,139,144,215-216`). F-Q231-4 is right that the service never sends them, but the UI can receive them. Bears on the count "98 occurrences of codes the backend never sends". Static reading; not exercised live. | source |
| **F-Q264-5** | 🟡 Medium | Auth and Platform in Development require RabbitMQ (`Transport: RabbitMQ`, Auth `appsettings.Development.json:59`, Platform `:60`); without it `/health` is 503 for the whole run. The dev runbook lists Mongo as the only prerequisite. | STACK.tsv |
| **F-Q264-6** | 🟡 Medium | The Development configs of Auth (`:44-45`), Gateway (`:46-47`) and Web (`:22-23`) carry a non-empty `JwtSettings:Secret` and five `PreviousSecrets`. The secret was overridden by environment here; the previous-secrets list cannot be emptied that way and was left. Not compared with the Q235 value; whether tokens signed with those keys are accepted was not tested. | config files (values not read) |
| **F-Q264-7** | ⚪ Info | Every saved page embeds the signed-in user's email and name (`window.CurrentUser`, `html/*.html:252`). The user is the seeded test identity. No log contains a token, password, the generated secret, a recipient name, note or evidence reference (scanned). | scan |
| **F-Q264-8** | ⚪ Info | Auth and Web binaries were 24 Aug while their sources were 2 Oct: a `--no-build` start (as `.claude/launch.json` does) would have served a Web app without the Shipment views. | STACK.tsv |
| **F-Q264-9** | ⚪ Low | `PermissionSnapshot.Has` matches literal keys only (`Services/PermissionSnapshot.cs:28-29`); `PermissionClaims.HasPermission` also accepts `*` (`Security/PermissionClaims.cs:17-19`). A `*` holder would get the denied page while the adapter lets the API through. Static; not exercised. | source |
| **F-Q264-10** | ⚪ Info | A parallel lane (Q266) ran `dotnet test` from `/Users/natig/mvp6-env/q266-…` against Mongo on 57061 during this WP. No port overlap. `git status --porcelain` moved 670 → 675 over the window; this WP's writes sit inside the already-untracked `docs/records/audits/2026-10/` entry and add none. The five new entries were not captured at start and are not attributed. | preflight / end |

## Not done

- No permission granted, no registry seeded, no user created, no product or config file edited.
- No golden flow, no state change (R-09 out of scope and impossible without keys).
- Arabic was exercised at mobile width only; desktop RTL not captured.
- RabbitMQ was not started and the transport was not overridden.

Nothing committed, nothing pushed.
