# Q394 — Web cross-cutting fixes: the JSON-401 challenge and the adapter deadline

- Lane: Q394 (REPLAN Phase 1), frontend-ui-ux, single-writer on `frontend/Diten.Web/Program.cs`. Recorded 2026-10-04.
- Preflight: `Sun Oct  4 13:45:14 UTC 2026` · `feature/mvp6-logistics` · HEAD `983dcd4d0` · porcelain 7 · staged 0.
  `Program.cs` was clean against HEAD.
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `frontend-ui-ux.md` `90247ddc689b1e06`
  - `MODULE-RECIPE.md` `49691537338d639e` (§2.4, §2.5, the shared-files table)

  All but the recipe are unchanged since my Q371/Q372-R2 reads, and the recipe is R-1's own sealed version.
- **Only `frontend/Diten.Web/Program.cs` changed (+64/−0).** Not touched:
  - `SupplyChainShipmentsController.cs` (Q372-R2 holds it);
  - `services/**`;
  - every module view and script.

  Nothing staged or committed. No token, password or secret printed. Agent verdict ≠ CT ACCEPTED.

## Verdict

| | before | after | sabotage | restored |
|---|---|---|---|---|
| Unauthenticated adapter call, 5 endpoints | 302 → `/account/login` | **401**, contract Error JSON, `X-Correlation-Id`, no `Location` | 302 ×5 | 401 ×5 |
| Unauthenticated page, 3 pages | 302 → `/account/login` | **302 → `/account/login`** | 302 | 302 |
| `ShipmentJsonAdapterChallengeTests` | red (302 vs 401) | **green** | red, "Expected Unauthorized, Actual Found" | — |
| Frontend suite | 163 / 1 / 164 | **164 / 0 / 164** | — | — |
| List load, SupplyChain frozen | 90 s (Q362) | **15.3 s** (adapter call 15 055 ms) | **90 095 ms** | **15 060 ms** |
| Create Save, SupplyChain frozen | (≤ 90 s, Ocelot) | **30.1 s** (30 088 ms) → "The request outcome is unavailable. Retry the same intent." | — | — |

**The frontend suite has zero failures: 164 / 0 / 164, the first time in this programme.** Q371, Q374, Q382 and
Q372-R2 each carried `Unauthenticated_adapter_uses_json_401_while_page_keeps_login_redirect` as "one pre-existing
failure". It now has a cause (R-1 F-R1-1) and a fix, and later lanes can stop carrying it.

The zero holds for Q394 alone, without Q372-R2's uncommitted controller edit:

- **Working tree** (with the Q372-R2 edit): 164 / 0 / 164.
- **Fixed `Program.cs` + HEAD controller:** 164 / 0 / 164 (`evidence/suite-results.txt`).

## Fix 1 — the marker gets its consumer (MODULE-RECIPE 2.4)

**CT's measurement, verified:**

- The attribute is a bare marker (`public sealed class JsonAdapterEndpointAttribute : Attribute;`).
- It is applied at `:53 :68 :76 :87 :101` of the controller.
- At HEAD, `Program.cs:72-78` sets `Cookie.Name`, `LoginPath` and `LogoutPath` and nothing else.

**What was lost, and where from.** The A12 base had the consumer. `~/mvp6-env/q185/treeA/src/frontend/Diten.Web/Program.cs:81`
reads the marker in `options.Events.OnRedirectToLogin`. Q202a imported the attribute and its test
(`mvp6-q202a-integration-write-01/WRITTEN-FILES.tsv:18,40`) but not this block, and not its `using Diten.Web.Security;`.

**The fix restores that block unchanged**, with a provenance comment. It does not invent a new design:

- **Marked endpoints** answer 401 with `{"error":{"code":"INVALID_REQUEST","message":"Authentication
  required.","correlationId":…},"contractVersion":"v1"}`. The correlation is the caller's if it is exactly one UUID,
  otherwise a new one, the same rule as the adapter's `ContractFailure`. It is echoed in `X-Correlation-Id`.
- **Every unmarked endpoint** keeps `context.Response.Redirect(context.RedirectUri)`, the framework default.

**Live proof** (`evidence/fix1-live-{fixed,sabotage,restored}.txt`), with no cookie, against the six-service stack:

- **Adapter calls:** `GET api`, `GET api/{id}`, `POST api`, `POST api/{id}/transition` and `POST api/{id}/pod` each
  returned **401**, `application/json`, the envelope above, `X-Correlation-Id` equal to the one sent, and no
  `Location`.
- **Pages:** `/SupplyChain/Shipments`, `/Create` and `/Details/{id}` each returned **302** to
  `/account/login?ReturnUrl=…`.

## Fix 2 — a deadline on the shipment-bundle family (MODULE-RECIPE 2.5)

**Blast radius, measured before choosing:**

| option | what it changes |
|---|---|
| `client.Timeout` on the default client | every consumer of the plain default `HttpClient`: **122** files take it in a constructor, and **14** more call `CreateClient()` unnamed |
| `ConfigureHttpClientDefaults` | all of the above **plus** the four typed clients, Auth, Branding, TenantStatus and TenantSlugResolver (`Program.cs:81-108`), two of which already set their own 5 s |
| per-request deadline in the controller | the controller is held by Q372-R2. Excluded |
| **chosen: a `DelegatingHandler` on the default client, active only for `/api/shipment-bundle/`** | **one caller today.** `grep -rln shipment-bundle frontend/Diten.Web --include=*.cs` returns only `SupplyChainShipmentsController.cs`. Every other URL passes through untouched |

The handler, `ShipmentBundleTimeoutHandler`, is declared at the end of `Program.cs`, inside the lane's one file:

- **15 s for GET, 30 s for any other method**, through a linked `CancellationTokenSource`.
- On expiry it throws `TaskCanceledException("Shipment-bundle call exceeded its N s deadline")`.
- The adapter's existing `catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)` answers that
  with 503 `PERSISTENCE_UNAVAILABLE`, so no controller change was needed.

**Why the prefix, not one controller.** The contract's server URL is `/api/shipment-bundle`, and the same document
holds `/carriers`, `/loads`, `/returns` and `/claims` (`shipment-bundle.openapi.yaml:21-22, 204, 410, 1364, 2215`).
The four later modules' adapters therefore get this deadline without editing `Program.cs` again, which is the point of
this lane.

**Live proof** (`evidence/fix2-timings.tsv`). SupplyChain was frozen with `SIGSTOP` on this lane's own process, as Q362
did. A killed service gets an instant 502 from the gateway and exercises no timeout.

| run | adapter call | which timeout fired |
|---|---|---|
| fixed, list | 15 055 ms; alert at 15.3 s | **ours.** Web log: `TaskCanceledException: Shipment-bundle call exceeded its 15 s deadline`, then the adapter's "request timed out" warning. Gateway: `RequestCanceled`, because the Web dropped the call |
| fixed, Create Save | 30 088 ms; message at 30.1 s | **ours**, the 30 s write deadline |
| **sabotage** (HEAD `Program.cs`), list | **90 095 ms** | **Ocelot's.** Gateway: `RequestTimedOutError … System.TimeoutException` at 90 s, then an empty 503, which the adapter wrapped (Q371) |
| restored, list | 15 060 ms | ours |

After `SIGCONT`, the healthy list call took **33 ms** through the handler. The shell's 17 nav links rendered; they are
served by default-client consumers, which the handler passes through.

### Q393 and the write number

CT's note: the unchanged-retry path returns 400 (R-1 F-R1-2), so a retry after a write timeout is not safe end to end.
**It does not change my number. 30 s stays.** Measured:

- **The frozen write did not commit.** On `SIGCONT`, SupplyChain finished that POST as **499** (client closed) in
  77 ms, and the database then held 0 shipments and 0 receipts for it. The page's same-key retry returned 201, giving
  **exactly 1** shipment (`evidence/fix2-write-after-resume-db.txt`, `fix2-supplychain-post-lines.txt`). So the deadline
  did not create a Q393 case here: Ocelot cancels downstream when the Web drops, and the service honours the abort.
- **The window is still there.** A write that commits after the deadline but before the abort reaches the service
  leaves a receipt. The page's retry then sends a fresh correlation (`create.js:76`) and gets 400. That window exists
  at **any** deadline, including the old effective 90 s. A longer number makes it rarer only by making every real
  failure slower to report.
- Healthy writes measured 5–440 ms (Q371), so 30 s is about 70× the slowest. Shortening it would widen the window, and
  lengthening it buys nothing measurable.

**Q393 must be fixed on its own terms**: the page resends the original correlation with the key, or the service
relaxes `ShipmentRepository.cs:70`. No timeout value substitutes for that.

## How it was run

- Scratch `~/mvp6-env/q394-20261004-1647/`.
  - **Web and Web.Tests:** a copy of the working tree (0 differences against the repository), built there.
  - **Services:** Auth, Platform, MDM, SupplyChain and Gateway ran from Q372-R2's already-built copy, checked equal to
    the repository source (0 differences, excluding the Development configs). This lane changes no service.
- One lane mongod on **57394** (`rsq394`); one throwaway JWT secret, mode 600, never printed.
- **Fixture:** users through Auth's invite endpoint and the Q185 org fixture. The tenant Admin's token carried
  `legal_entity_id` and all 5 Shipment keys.
- The browser signed in with the password served to the page by a local helper; no value passed through the
  transcript. It was signed out at the end.
- **Sabotage copy** `sab/`: `Program.cs` = `git show HEAD:` (diff: 64 lines). Built separately, swapped in on 5001,
  then swapped back.
- **Stopped at the end by recorded pid:** web, gateway, supplychain, mdm, platform, auth, the helper, then the lane
  mongod. Ports 5000, 5001, 5056, 5057, 5059, 5061, 5199 and 57394 are free.
- **Q372-R2's suite mongod on 57373 was left running**, as that paused lane left it.

## Findings

- **F-Q394-1 — the handler covers future modules only through this URL prefix.** A later adapter that calls a
  different path family gets no deadline. Recipe 2.5 should say: "use the `/api/shipment-bundle/` family, or register
  the handler for yours."
- **F-Q394-2 — Ocelot has no QoS timeout configured, so its 90 s default stands.** Every non-shipment Web consumer that
  calls the gateway still waits up to 90–100 s for a frozen service. Out of scope here (blast radius above); recorded.
- **F-Q394-3 — my first live probe was invalid.** zsh passed `--data {}` as one word, so curl rejected the POSTs and
  those three lines showed the previous response's temp files. It was redone under bash, and the file was overwritten
  before anything read it. Noted because the bad output looked plausible.
- **F-Q394-4 — the copy trap, hit again.** Running the suite in a copy without the provider sources gave 160/4/164, all
  four from `NavManifestL10nGuardTests`. That is Q382 F-Q382-5 and MODULE-RECIPE 8.1; it was resolved by adding the
  sources, not by changing anything.
- **F-Q394-5 — the Web log line carried the correlation id** ("request timed out …; correlation 4a64fc50…") only
  because the working-tree controller includes Q372-R2's uncommitted logging edit. Q394 does not depend on it.

Nothing committed, nothing pushed, nothing staged.
