# Q235 — Consequences of the three options

Numbers are measured (`ARCHIVE-INVENTORY.tsv`). "Archives" means the **17** untracked archives under `docs/`
that carry the value: Q230's 16 plus `mvp6-mod0185-fresh-evidence-ver-03/evidence.tar.gz`.

## Facts all three options share

| Fact | Value |
|---|---|
| Archives whose own folder has a checksum file naming them | 16 of 17 (`mvp6-mod0190-test-oracle-rework-01/` has none) |
| References to these 17 archive hashes in other folders' records | 178, across `docs/` (`.md`, `.tsv`, `.txt`, `.json`, checksum files) |
| Most-cited | `successor-source.tar.gz` `7b6a0d1a…` — 49 outside references; `BC-SOURCE.tar.gz` `ebd5d80c…` — 35 |
| History rewrite | Not allowed (`mvp6-q147-owner-decision-pack-01/DECISION-PACK.md:71`), so a commit is permanent |
| The value is already in the local object database | `GIT-HISTORY-CHECK.md` |

## (a) Keep the archives out of git

**Checksum files that break in git** — 16 folders' own `SHA256SUMS` would name a file the repository does not
contain (the F-Q230-8 effect). Checking them in a clone reports the archive as missing; every other line still
verifies.

**BASE-STACK v2 chain links that lose their source**

| Link | Archive | Where it is pinned |
|---|---|---|
| BASE input L1 (BC-SOURCE, 422 files) | `mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` `ebd5d80c…` | `mvp6-q103-accepted-base-01/README.md:18`, `COMPOSE-LOG.txt` |
| BASE input L2 (A12-360, 360 files) | `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` `7b6a0d1a…` | `mvp6-q103-accepted-base-01/README.md:19`, `COMPOSE-LOG.txt` |

BASE-STACK v2's own files (`BASE-STACK-v2.md`, `LAYERS.tsv`) pin the BASE **manifest** hash and the Q117 / Q121 /
Q131 overlays, not these two archives. The Q117, Q121 and Q131 overlay archives do not carry the value and are
not affected.

**Is BASE-STACK v2 still reproducible without them?** Measured member by member against the working tree and `HEAD`:

| Input | Members | Byte-equal copy exists in the tree or in `HEAD` | Exist only in the archive |
|---|---:|---:|---:|
| L1 BC-SOURCE | 422 | 413 | 9 |
| L2 A12-360 | 360 | 343 | 17 |

Of the 17 L2-only members, 8 are replaced later in the stack anyway (five CapacityPlans tests by Q117/Q131, the
three probes by Q117). The other **9 exist nowhere else in the repository**: three gateway test files,
`ocelot.json`, `SupplyChainService.Api/Program.cs`, `CrmService.Api/Program.cs`, `frontend/Diten.Web/Program.cs`,
`.antigravity/rules/ports.md` and the MOD-0183 pack. These are exactly the held and shared-seam paths of Q202a / Q209.

So: **the final stack can be rebuilt from git for all but 9 files; the manifest check "14,566 of 14,566" cannot be
re-run for 26 members.** With the archives kept in a store outside git, nothing is lost.

**What stays true:** nothing irreversible happens. The archives remain on disk and in the Q198 backup.

## (b) Commit as-is after confirming the value is dead

| Point | Consequence |
|---|---|
| Integrity | Nothing breaks. All 17 hashes, 16 checksum files and 178 references keep verifying |
| "Confirming it is dead" | Not possible from the repository alone. `LIVE-OR-DEAD.md` reaches DEAD in the tree and CANNOT DETERMINE outside it. The confirmation has to come from whoever knows every environment that ran the service |
| Residual risk if it is dead everywhere | A known test credential sits in history permanently. It grants nothing by itself |
| Residual risk if it was ever configured on a running service | Anyone with the repository can mint tokens that service accepts — any tenant, any permission. It is an HMAC secret: knowing it is enough |
| Reversibility | None. No history rewrite is allowed. After a push it is also on GitHub |
| Recorded decision | The decision pack's rule is "a secret must never enter a commit" (`DECISION-PACK.md:71`). Option (b) needs the owner to set that sentence aside in writing |
| `.antigravity/rules/security-jwt.md` | Line 34: signing keys may not be embedded in code "even in a Dev environment". Line 14: secrets are never hard-coded. The rule has no exception for dead or test values and does not mention archives of old code. Read strictly, the archived files are copies of code that broke the rule; committing them puts that code into history. **Not acceptable under the rule as written** without an explicit owner exception |
| Scope | 5 further file versions carry the value (2 in the 17th archive: `mod0185-fresh-recovery.py`, `mod0185-fresh-startup.py`). They would enter history too |

## (c) Commit redacted copies

Redaction changes bytes; repacking changes more than the three files (gzip and tar metadata).

| What breaks | Count / detail |
|---|---|
| Archive hashes | all 17 |
| Own-folder checksum files | 16 fail on their archive line |
| References in other records | 178 no longer match |
| BASE manifest | 3 rows: `BASE-MANIFEST.tsv` lists the pre-fix probe hashes (`e5ee0aed…`, `8f6cc448…`, `296c150a…`) as BASE content |
| Q117 overlay | `OVERLAY-MANIFEST.tsv:8-10` `base_sha256` for the three probes no longer matches its preimage; the compose recipe's "verify before use" step stops (`BASE-STACK-v2.md` §4 step 1) |
| Q103 inputs | `README.md:18-19` and `COMPOSE-LOG.txt` hashes for L1 and L2 |
| Accepted records | CT-accepted records are not edited (K4, `BASE-STACK-v2.md:10`). Repairing the pins means new records and a new stack version, not corrections |
| The final stack bytes | Unchanged: Q117 overwrites the three probe files, so no redacted byte reaches the composed tree |
| Work | Redact 3 members in 16 archives and 5 members (one nested level) in the 17th; re-derive every hash; write the superseding records |

Redacted copies are **new artifacts**. They cannot stand in for the accepted ones; they would sit beside a
statement that the originals exist elsewhere.

## Side by side

| | (a) keep out | (b) commit as-is | (c) commit redacted |
|---|---|---|---|
| Value enters a pushed commit | no | **yes, permanently** | no |
| Integrity chain in git | 16 checksum files incomplete; 9 stack files not in git | intact | 17 hashes and 178 references broken |
| Reversible | yes | **no** | yes, at the cost of new records |
| Needs an owner exception to a recorded rule | no | yes (two rules) | no |
| Depends on a fact nobody can establish from the repo | no | **yes** (dead everywhere) | no |
