# Q235 — Recommendation (not acted on)

**Decision owner: the repository owner** — the person who decided OD-Q03a and OD-Q03b. CT records the decision;
the security-agent view is advisory.

## Recommended: option (a), with three additions

Keep all **17** archives out of git, and:

1. **Custody outside git.** Name one store for the 17 originals (the Q198 backup already holds them). The chain
   then verifies by hash whenever an archive is supplied.
2. **A held-artifacts manifest in git.** One small file listing the 17 paths and sha256 values and saying where
   the originals are kept, so a missing file in a clone reads as "held", not "lost". `ARCHIVE-INVENTORY.tsv` has
   the data.
3. **Extend the hold.** Q230's HOLD-F02 list has 16 entries; add
   `docs/records/audits/2026-09/mvp6-mod0185-fresh-evidence-ver-03/evidence.tar.gz`.

## Why

- **The two outcomes are not symmetric.** Holding can be undone on any day. A commit cannot: history rewrite is
  not allowed, and after a push the value is on GitHub.
- **Option (b) rests on a fact nobody can establish from the repository** — that no running service was ever
  configured with the value. For an HMAC signing secret, being wrong means forgeable tokens.
- **Two recorded rules say no** to (b): the decision pack ("a secret must never enter a commit") and
  `security-jwt.md` line 34. Setting them aside is the owner's call, not an agent's.
- **Option (c) costs the most and proves the least.** It breaks 17 hashes and 178 references, and the redacted
  files are new artifacts that cannot replace the accepted ones.
- **Option (a) loses little.** Only 9 stack files exist solely in the archives, and they are the shared-seam
  files Q209 has to deliver anyway.

## Separate from the ruling, but found by it

These do not depend on which option is chosen.

| # | Item | Why | Who |
|---:|---|---|---|
| 1 | Run `git ls-remote origin 'refs/codex/*'` | The value is in the local object database through nine `refs/codex/…` refs. Nothing local says they were pushed; only the remote can confirm | owner / CT |
| 2 | Decide what to do with the nine `refs/codex/turn-diffs/checkpoints/…` refs and the five dangling blobs | While the refs exist, `git gc` keeps the value. Removing refs is a git write and needs explicit approval | owner |
| 3 | Never push with `--mirror`; never publish the `.git-backups` bundles | Both carry `refs/codex/*` | owner |
| 4 | Confirm with operations that no environment ever ran with this value as `JwtSettings__Secret` | Turns CANNOT DETERMINE into DEAD or LIVE | owner / operations |
| 5 | Re-run the commit plan's secret scan with nested archives included | Q230's scan missed the 17th archive because it did not open nested archives; other values may hide the same way | CT (Q230 successor) |

## If the owner prefers (b)

It becomes defensible only after item 4 comes back clean **and** the owner records an exception to both rules.
Even then the value stays in history for good.

Nothing in this recommendation was carried out.
