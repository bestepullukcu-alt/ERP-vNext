# Q322 wave — what this lane did not touch, and why

Three kinds of thing are listed here instead of being changed. All of them are measured.

## 1. §22 text that describes the UI as unbuilt, while the tree has it built

§22 carries its own owner authority — `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md`
(D1–D5 = A) — and Q320's precedent was to reconcile §9 and §11 *to* §22, not to rewrite §22. So these
sentences are left exactly as they stand and put to the owner:

| pack | location | the sentence | what the tree holds |
|---|---|---|---|
| MOD-0185 | §22 note 1 | "Loads UI scope is HELD and unbuilt; the RoutePath and the action table are proposals and must be re-checked against the approved UI before this section is applied." | `SupplyChainLoadsController.cs:20` routes `/SupplyChain/Loads`, view action `:43`; six files under `Views/SupplyChain/Loads/`; 62 l10n keys in each of seven languages |
| MOD-0185 | §22 M-04 | "…the frontend view-route set of the Loads controller (to be built)…" | the controller exists |
| MOD-0186 | §22 page table | RoutePath cell reads `/SupplyChain/Returns` **"(approved; not a route yet)"** | `SupplyChainReturnsController.cs:35` declares that route, view action `:75` |
| MOD-0186 | §22 M-04 | "…the Returns controller (to be built)…" | the controller exists and was driven live in the R-2 lane |
| MOD-0187 | §22 M-04 | "…`SupplyChainClaimsController` (§32.4; to be built)…" | `SupplyChainClaimsController.cs:29` exists and was driven live in the R-4c lane |

The question for the owner is narrow: **does the Q322 wave also strike these four qualifiers, or does §22
stay as written until its own revision?** Q320 did not face it because MOD-0183's §22 was already current.

Nothing downstream is blocked either way: §9 and §11 as proposed describe the tree, and M-04's *check* still
passes — the manifest RoutePath set and the frontend view-route set are both exactly one per module, which is
what M-04 compares. It is the parenthetical prose that is stale, not the test.

## 2. MOD-0185 has no view model, and whether it should is a design decision

Measured: nothing under `frontend/Diten.Web/Models/` matches `*Load*`, and
`Controllers/SupplyChainLoadsController.cs` references no view model type. The offcanvas is driven from
JavaScript against the API with fields identified by `id`. The other three modules in this wave each have one.

The proposed §11 records "none" rather than inventing a file. Making Loads match its siblings is a change to
built UI, which this lane does not make.

## 3. Cells that are open but are not CT's to close

| cell | why it is not this lane's |
|---|---|
| MOD-0186 / MOD-0187 row 3, Contract blocker | the pack's routing contract is contradicted by the running gateway catch-all, and `gateway/**` is Protected Paths in every pack — integration owner |
| MOD-0184 row 13 | `:376` withholds MOD-0184 runtime write; observation was always permitted and the row is MET as of 2026-10-12, but the pack-required write signals are the owner's |
| MOD-0187 `claimLinkCarrier` | Q424: the claim carrier path depends on a field no contract writes. A UI control that can never act is a contract gap, not a UI defect |

## 4. The nine gate cells this wave does not close

Rows 2 (×4), 7 (×2) and 12 (×3) are browser-only: Save View and column visibility exercised live, a
client-side validation rejection, an empty list, a validation-error state. Measured per cell in
`docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01/GATE-18-0.tsv`. The proposed §11 sections
list them as open items so that approving the revision does not read as closing them.
