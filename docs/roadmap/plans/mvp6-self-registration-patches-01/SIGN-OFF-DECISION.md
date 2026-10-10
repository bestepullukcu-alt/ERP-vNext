# Sign-off decision text — Supply Chain self-registration patches (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** Nothing here is signed off. An agent quoting this text is not approval.
The owner signs off by stating the text below, or a named subset, in their own words.

## Patches and the files they bind to

| # | Patch | Target | Before (must match) | After apply | Section added |
|---|---|---|---|---|---|
| 1 | `patches/01-DCP-009-self-registration-foundation.patch` | DCP-009 | `ce30e922…d0ea77d083` | `e346043d…47020cec6` | §21 follow-up (foundation) |
| 2 | `patches/02-MOD-0183-self-registration.patch` | MOD-0183 | `32381634…7eaf962e` | `8e269efa…42eadf` | §22 |
| 3 | `patches/03-MOD-0184-self-registration.patch` | MOD-0184 | `2df9363b…7de817e` | `346288ab…c1bffd1e` | §31 |
| 4 | `patches/04-MOD-0185-self-registration-after-loads-ui.patch` | MOD-0185 | `48686693…0307536f` | `45b5dd33…c27caa5` | §29 (after Loads UI approval) |
| 5 | `patches/05-MOD-0186-self-registration.patch` | MOD-0186 | `07a8a015…614a04b7` | `564bb40e…c517f28` | §30 (rebase if Q39 lands first) |
| 6 | `patches/06-MOD-0187-self-registration.patch` | MOD-0187 | `762ab533…c7786b` | `31cb35c3…ad06626` | §33 |

Full hashes are in `BASE-HASHES.tsv`; patch checksums in `SHA256SUMS`.

## Decision text to record

> I sign off the Supply Chain self-registration patches in `docs/roadmap/plans/mvp6-self-registration-patches-01/` (SHA256SUMS sha256 `<to be filled by CT from the committed file>`): **[all six | patches #…]**.
>
> - Each approved patch is applied only if its target file still has the "Before" sha256 above and the result has the "After apply" sha256. If a target has changed, the patch is rebased and brought back to me; it is not applied by hand.
> - Patch 5 (MOD-0186) is bound to `07a8a015…`. If the Returns sign-off (Q39) is applied first, patch 5 is rebased onto the new base (its section becomes §33) and re-checked before it is applied.
> - Patch 4 (MOD-0185) records a section that stays inactive until the Loads UI scope is approved and built.
> - Applying the patches changes pack and DCP text only. It authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash by an agent. Code remains with the single CT-appointed integration owner after an integrated target (Q14/Q15) exists, shipping each module's provider with its UI and nav keys (D4).
> - The open gaps recorded in the sections stay open.

## Options for the owner

| Option | Meaning |
|---|---|
| **A (recommended):** all six | Apply 1, 2, 3, 6 now; 4 as an inactive section; 5 now if Q39 has not landed, otherwise after rebase |
| B: all except 4 | Hold the Loads section until the Loads UI decision |
| C: hold all | Wait for the Returns sign-off (Q39), then rebase 5 and sign off all six together |
