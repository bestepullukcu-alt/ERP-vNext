# Secret untrack — closing measurement against HEAD

- date: 2026-10-03
- owner: Control Tower
- ledger: Q287, Q303, Q340, Q341, Q342, Q343
- method: parse every tracked `appsettings*.json` **blob in HEAD** (`git show HEAD:<path>`),
  read `JwtSettings.Secret`, hash it. Never the file on disk — that is the mistake this
  record exists to prevent.

## Result

| | |
|---|---|
| tracked `appsettings*.json` blobs in HEAD | 45 |
| carrying `JwtSettings.Secret` | 10 |
| **LIVE (shipped source)** | **0** |
| under `.tmp-*` build output | 10 → Q341 |

## The three commits

| sha | what |
|---|---|
| `987b9c9d3` | 9 Development configs untracked, 11 `.example.json` added, `.gitignore` updated |
| `fa791f07f` | the two the first pass missed — `frontend/Diten.Web` (denied at the time) and `Diten.EnterpriseStrategy.API` (CT mistyped the path as `…StrategyService.Api`) |
| `8f60dc6d3` | the two **base** configs Q303 cleaned on disk but never committed |

## What this does NOT close

1. **Rotation (Q293).** Untracking removes a file from future commits. It does not remove it
   from history, and it does not invalidate anything. All seven retired digests remain valid
   signing keys until they are rotated.
2. **Q341** — 797 tracked `.tmp-*` build files, ten of which are these copies.
3. Step 1's **code** commit — 681 dirty paths, untouched by these three.

## The rule this writes

A lane reporting an in-place edit has reported a **working-tree** fact. Q303 did the edit and
reported it truthfully; CT confirmed it against the file on disk and agreed. Both were right
about the disk and both were wrong about the repository for two commits. Cleanup claims are
verified against `HEAD`.
