# KIT-SPEC delta — v1.1 (proposal-02) → v1.2 (proposal-03)

This page lists only what changes against v1.1. The v1.1 delta (`mvp6-evidence-kit-proposal-02/KIT-SPEC-DELTA.md`) and
the v1.0 spec (`mvp6-evidence-kit-proposal-01/KIT-SPEC.md`) stay valid for everything not named here. The source of each
change is the Q63 independent review (`docs/records/audits/2026-09/mvp6-evidence-kit-v1-1-independent-review-01/SOP-22-VER.md`).

## K04 — key pairing from product source (F1)

- `key-pairing.tsv` is new. It lists the 30 keys that must carry one value per lane, grouped as follows. Each key is
  cited from product source as `path:line=token`, 57 citations in all; the layer column records whether the cited file
  comes from HEAD or one of the two sealed a08 overlays.
  - `auth-platform`, 6 keys: every key that Platform checks against `AuthService:InternalApiKey`, plus the Auth key
    that Platform calls with. This now includes MDM `PlatformRegistration:InternalApiKey` (the audit forwarder).
  - `platform-mdm-active`: Platform `ModuleRegistrationCredentials:Mdm:ActiveSecret` = MDM
    `PlatformRegistration:ModuleRegistrationCredentialSecret`.
  - `svcid` and `jwt`.
  - Non-secret pairs: the registration identifier, and the service-identity `KeyId`, `Issuer`, `Audience`, `CallerId`
    and `Enabled`.
  - Single keys: `mfa` and `platform-mdm-previous`.
- `service-map.tsv`: the MDM row renders `PlatformRegistration__InternalApiKey` as `auth-platform` and adds
  `PlatformRegistration__ModuleRegistrationCredentialSecret` as `platform-mdm-active`.
- `check` no longer compares the kit's own group labels. It does four things:
  - verifies every citation in the K01-composed source. A token on the cited line counts as cited; a token elsewhere
    in the same file counts as moved and is recorded; an absent file or token is NOT FOUND and fails closed.
  - refuses any rendered secret key that is not in the table;
  - compares the effective value each lane service will use: appsettings < user-secrets < env file, and the class
    default for value pairs. A secret group must resolve to one lane placeholder; a value group must resolve to one
    non-empty value.
  - writes the results to `#pairing` rows (group, kind, members, result) and to `raw/key-pairing-citations.tsv`.
- `tests/test_k04_pairing.py` is new. The v1.1 wrong table must FAIL, the v1.1 `REQUIRED_PAIRS` without citations fail
  closed, and v1.2 must PASS.

## K00 — supervisor (F2, F4, F6, F7, F8, F10, F14)

| Area | v1.1 | v1.2 |
|---|---|---|
| `run` | any argv, any cwd, all values | allow-list: `k07` (actor passwords), `scan`/`seal` (all values), `harness` (a .js/.py file in `EK_HARNESS_DIR`, validated args, actor passwords) |
| peers | anyone who can open the socket | kernel peer credentials must match the supervisor's user; refused if they are unavailable |
| `reveal` | password returned over the socket; TTY check in the client only | `reveal-arm` writes a one-time code to the caller's own tty; `reveal` with that code writes the password to that tty; nothing over the socket; no tty → refused |
| lifecycle | `shutdown` only | + `abort` (stops its own services, clears values, removes socket); self-expiry after `--max-lifetime` (12 h) or `--idle` (4 h) |
| socket | unlinks any existing path | folder must be the user's own and not group/world-writable; existing path must be the user's socket and not live |
| logs | fallback `<sock>-logs` left behind | removed at shutdown, abort and expiry |
| core dumps | OS default | `RLIMIT_CORE = 0`, inherited by every child |
| requests | unbounded | ≤ 64 KiB, 10 s; still served one at a time (serialisation is intended) |
| launch | listener PID = child PID | + every listening address must be 127.0.0.1 (`listen_addrs` column in `processes.tsv`) |

## Driver (F4)

- `run-kit.sh up` refuses when a live supervisor answers on `$EK_SOCK`.
- Any failure inside `up` runs the abort path and exits non-zero: supervisor `abort`, then K11.
- The new `run-kit.sh abort` runs the same path by hand.
- K07 and the seal run as supervisor tasks (`ek_ctl run k07`, `ek_ctl run seal`).

## K08 (F3, F9)

- Containers are detected by magic bytes and opened recursively, to depth 4: zip (including Playwright traces), gzip,
  tar and tar.gz.
- Anything the kit cannot open is a HIT marked "unscannable": bzip2, xz, zstd, 7z, rar, an encrypted member, a corrupt
  archive, or a member over 512 MiB.
- New exact forms: alignment-independent base64 cores, so a value inside a larger base64 body is found at any byte
  offset.
- `--seal` verifies ARTIFACTS.sha256. Every entry must exist with its hash, and every evidence file must be listed
  (except ARTIFACTS.sha256 and SECRET-SCAN-FINAL-*). The report gains an `artifacts_verified` line.

## K01 (F5, F13)

- git runs through `git_argv`: read-only `rev-parse`, `status` and `archive`, with `--no-optional-locks`.
- Wrapper rule: strip the single top folder T only if T is not a HEAD top-level name and no path in the overlay's
  manifest starts with `T/`. If every path starts with `T/`, T is kept, so a genuinely new top-level folder survives. A
  mix of both is refused.

## K10 (F12)

- `--totals-exempt <collections>` together with a mandatory `--exempt-reason`. The exempted deltas and the reason go
  into the new `totals_exempt` column of `db-assertions.tsv`.

## K11 / shell library (F5, F7)

- `ek_git`: an allow-list of read-only subcommands, with `GIT_OPTIONAL_LOCKS=0` exported.
- `EK_SOCK` defaults to the per-user `$TMPDIR`. If `TMPDIR` is unset and `EK_SOCK` is not given, the kit refuses to
  start.

## Accepted, not changed

- **F11**: phase and step rows can appear out of order. Every row carries a start and end time, and the guide says so.
- **F10**, the single-threaded part: lane operations are serialised on purpose.

## Q24 validation, added conditions

- `raw/key-pairing-citations.tsv` has no NOT FOUND.
- Platform accepts MDM's module registration, and at least one MDM audit append returns 2xx. K04 proves the
  configuration; only a live call proves the product path.
