# Q339 — SupplyChain module self-registration — STOPPED before any edit

- Lane: Q339, integration-agent. Recorded 2026-10-03 (preflight `Sat Oct  3 16:11:42 UTC 2026`).
- Preflight: branch `feature/mvp6-logistics`; HEAD `8f60dc6d3`; `git status --short | wc -l` = 680; staged = 0.
- Step 0 read: `AGENTS.md`, `.antigravity/rules/git-safety.md`, `.antigravity/rules/code-style.md`,
  `.antigravity/agents/integration-agent.md`.
- **State: STOPPED. No product file changed.** `Program.cs`, `appsettings*.json` and every file under
  `services/**` are byte-for-byte as found. No service booted, no test run, no secret printed. The only file this
  lane wrote is this report.

## Why it stopped — the dispatch conflicts with a module pack that outranks it

The dispatch asks for both providers to be registered (`AddSingleton<IModuleManifestProvider, …>() ×2`, and P3a
depends on two). One of them is `CarrierManagementManifestProvider` (MOD-0184). Its pack forbids exactly that line
today:

- `execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md:473` (§22 Ship rule, D4 = A):
  "The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with
  this module's UI** in the integrated target (Q14/Q15), never ahead of it."
- The same pack, `:6`: `shell: none`.
- The same pack, `:418`: "This section specifies; it authorizes no code, `Program.cs`, …, appsettings … change".
- Carrier has **no UI at all**: `find frontend/Diten.Web -ipath '*carrier*'` returns 0 files, and
  `frontend/Diten.Web/Views/SupplyChain/` holds only `Shipments`.
- The provider pushes a page with `RoutePath: "/SupplyChain/Carriers"`
  (`…/ModuleRegistration/CarrierManagementManifestProvider.cs:26`). Once registered, the Platform catalog would list
  a page that the web does not serve.

AGENTS.md §1 puts the Module Pack above everything else, and the dispatch names that order as its own authority.
So the lane cannot register the Carrier provider. Registering **only** the Shipment provider would be a different
design from the one dispatched (P3a's "only ONE manifest is sent" would become the normal state, not the sabotage).
That choice belongs to CT or the owner, not to this lane.

MOD-0183 (Shipment) has the same ship rule
(`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:572`) and the same "authorizes
no `Program.cs` change" line (`:511`). Its UI does exist in the working tree, untracked. So a Shipment-only
registration is arguably "together with its UI" in the working tree. Whether the owner's commit would ship them
together cannot be decided from here.

## The five CT facts

| # | verdict | what is actually there |
|---|---|---|
| 1 | CONFIRMED | `ModuleRegistration/` holds `IModuleManifestProvider.cs`, `ModuleRegistrationHostedService.cs`, `PlatformRegistrationOptions.cs`, `CarrierManagementManifestProvider.cs`, `ShipmentTrackingPodManifestProvider.cs`. Addition: all five files are **untracked** (`git ls-files` on the folder returns nothing) |
| 2 | CONFIRMED | `Program.cs:84` `AddHostedService<ShipmentOutboxWorker>()`; no `ModuleRegistration`, `PlatformRegistration` or `IModuleManifestProvider` in the file. Addition: `Program.cs` is already modified and uncommitted (+64/−2 vs HEAD, `git diff --numstat`); this lane did not author those changes |
| 3 | CONFIRMED | `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:88-92`: Configure → AddHttpClient → AddSingleton ×2 → AddHostedService, in that order. DevEnablement `Program.cs:39-43` is the same shape; `:43` is the `AddHostedService` line |
| 4 | CONFIRMED | `ModuleRegistrationHostedService.cs:18,24` takes `IEnumerable<IModuleManifestProvider>`; `:52` loops over every provider |
| 5 | **FALSE in one cell** | HEAD `appsettings.json`: **no `PlatformRegistration` section at all** (CT: `BaseUrl=''`, key len 0). Working tree `appsettings.json`: `BaseUrl='http://localhost:5057'`, key len 0 — CONFIRMED, an uncommitted edit (`git diff --numstat`: +12/−3). `appsettings.Development.json`: no section — CONFIRMED (file is gitignored, `.gitignore:25`). MDM `appsettings.Development.json`: `BaseUrl='http://localhost:5057'`, key len 45, sha256 first-8 `e96ab27d` — CONFIRMED. Addition: `appsettings.Development.example.json` (tracked) also has no section |

## Step C — does the service boot with PlatformRegistration unconfigured? (read, not run)

Yes, by reading. `ModuleRegistrationHostedService` is a `BackgroundService` (`:13`):

- `:44-48`: if `BaseUrl` or `InternalApiKey` is blank, it logs a warning and returns. No exception is thrown.
- `:60-86`: each provider is wrapped in a try/catch. Failures are logged and never rethrown.
- `:136-145`: an unreachable Platform (`HttpRequestException`) is retried 5 times with 2/4/8/16 s backoff
  (`:16`, `:65`) and then abandoned with a warning (`:69-75`).
- `:115-130`: 4xx stops without retrying. 5xx retries.

So neither an unconfigured section nor a dead Platform should stop the API from serving. **This is a reading, not
proof.** P1–P3 were not run.

One consequence for the dispatch's own P2/P3b design: the working-tree base `appsettings.json` sets a BaseUrl but an
empty key. Without the Development file supplying the key, the service skips registration silently at `:44`. P2 would
then show no outbound attempt at all, rather than a redacted header.

## Proof P1–P4 — NOT RUN

- P1 (three boots), P2 (outbound call), P3a/P3b (sabotage): not run. Each needs the registration lines, which the
  STOP prevented.
- P4: **the named baseline does not exist.** `docs/records/2026-10/` contains only `mvp6-secret-untrack-01`; there is
  no `mvp6-q335-suite-baseline-01`. The 432/1/433 figure cannot be compared against a record. The latest persisted
  SupplyChain run this lane knows of is Q266 (`docs/records/audits/2026-10/mvp6-q266-testenv-01/PER-MODULE.tsv`:
  417/1/418).

## Files changed by this lane

| file | sha256 |
|---|---|
| none under `services/**`, `frontend/**`, `gateway/**` | — |
| this report | see `ARTIFACTS.sha256` beside it |

## Findings

- **F-Q339-1** `…/MOD-0184-carrier-management.md:473` vs dispatch step A: registering the Carrier provider ships its
  `AddSingleton` line ahead of a UI that does not exist (`shell: none`, `:6`). Pack outranks the dispatch.
- **F-Q339-2** CT fact 5, HEAD cell: HEAD `appsettings.json` has no `PlatformRegistration` section, not an empty one.
- **F-Q339-3** P4 baseline record `docs/records/2026-10/mvp6-q335-suite-baseline-01/` does not exist.
- **F-Q339-4** All five `ModuleRegistration/*.cs` files and `Program.cs`'s current edits are uncommitted.
  `Program.cs` is already +64/−2 vs HEAD from other lanes. This lane's single-writer claim on `Api/**` sits on top
  of edits it did not make.
- **F-Q339-5** Record path `docs/records/2026-10/` is not one of the `records/` sub-folders that
  `.antigravity/rules/docs-organization.md:45-49` and `:102` name (`audits/`, `analysis/`, `acceptance-reports/`,
  `releases/`, `decisions/`). It was used because the dispatch names it and the folder already exists.
- **F-Q339-6** With the working-tree base config (BaseUrl set, key empty), registration is skipped silently
  (`ModuleRegistrationHostedService.cs:44`). The Development file must carry the key for P2 to be observable.

## What CT or the owner needs to decide (this lane decides none of it)

1. Register both providers anyway: needs an owner decision that overrides MOD-0184 §22 D4 = A.
2. Register Shipment only: a re-dispatch with P3a reshaped.
3. Hold until the Carrier UI exists.

## Committed

Nothing committed, nothing pushed, nothing staged.
