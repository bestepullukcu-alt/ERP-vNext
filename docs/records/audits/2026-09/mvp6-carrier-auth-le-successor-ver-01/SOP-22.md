# MVP6-CARRIER-AUTH-LE-SUCCESSOR-VER-01 — SOP §22

Date: 2026-09-23  
Role: independent verifier; candidate/source writer: no  
Branch: `feature/mvp6-logistics`  
HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **REWORK — PHASE 2 NOT RUN**

## 1. Scope and gate

This verification consumed the exact package under
`docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-01/` and the existing
native .NET 8 environment handoff. It did not change Auth, Platform, MDM, Carrier,
Gateway, permissions, contracts, packs, `.antigravity`, configuration or Git.

Phase 1 was completed against the immutable 22-file candidate overlay. Phase 2 was
not started because both mandatory gates remain open:

1. `OWNER-DECISION-TEXT.md` is explicitly `UNAPPROVED`.
2. The visible writer workspace has no immutable final full-source archive,
   final target manifest, writer-complete record or no-active-writer handoff.

The 22-file `source-archive.tar.gz` is an overlay and is not a complete build tree.
The mutable writer workspace is not an acceptable independent verification input.

## 2. Exact input integrity

| Artifact | Expected / observed SHA-256 | Result |
|---|---|---|
| `successor.patch` | `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673` | PASS |
| `SOURCE-MANIFEST.tsv` | `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d` | PASS |
| `source-archive.tar.gz` | `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846` | PASS |
| `evidence.tar.gz` | `091f31450bec79797b5faeb5d93ba8ff9bdcbd657c9d3e403339abeb41ddf00a` | PASS |
| Root manifest | 9/9 entries | PASS |
| Overlay targets | 22/22 target hashes | PASS |
| Patch baseline applicability | `git apply --check`, exit 0 | PASS |

## 3. Phase 1 findings

### F-01 — exact token cardinality is not enforced — BLOCKING

`PlatformServiceTokenValidator.cs:45-70,83-84` uses standard
`ValidAudience` and `ClaimsPrincipal.FindFirst`. A correctly signed token can carry
the expected first `sub`, `scope`, `tenant_id`, `actor_id` or `legal_entity_id`
claim and an additional conflicting claim. Similarly, a token containing the
expected audience plus an additional audience is not explicitly rejected.

This does not satisfy the controlling requirement that caller, audience, scope and
the tenant/actor/LE tuple be exact. The existing focused tests cover wrong singular
values, not duplicate/conflicting claims or multiple audiences.

**Required rework and closure test:** require exactly one value for every security
claim and exactly one expected audience. Add signed negative tokens for every
duplicate/conflicting claim and expected-plus-extra audience; each must return 401
before mediator/repository access. Preserve the valid exact-token case.

### F-02 — lifetime anchor/skew behavior is incomplete — BLOCKING

`PlatformServiceTokenValidator.cs:51-52,67-70` validates normal token lifetime and
checks only `exp - iat <= MaximumTokenLifetimeSeconds`. It does not explicitly bind
`iat` to `nbf` or the current acceptance window. A signed token with a future `iat`
and an earlier acceptable `nbf` is not rejected by this custom check. Configurable
clock skew also means a just-expired token may remain valid.

**Required rework and closure test:** record the exact accepted relationship between
`iat`, `nbf`, current time, `exp` and configured skew; reject inconsistent/future
issuance and excessive effective lifetime. Run separate negatives for future `iat`,
future `nbf`, expired inside/outside the declared skew and excessive lifetime. This
is a precision correction to the approved fail-closed intent, not a new MDM grant.

### F-03 — broad service trust boundary must remain explicit

The Platform internal key is checked before tenant/actor binding, and wrong-key
requests cannot call the resolver. After a valid key, the caller chooses route
tenant/user; the tenant-scoped Platform resolver supplies actor-to-LE authority.
Platform's MDM token provider can mint a token for any non-empty tuple supplied by
trusted Platform code. MDM proves token authenticity and exact tuple/path binding;
it does not independently prove actor entitlement.

Therefore a correct key or service token alone is service authentication, not
tenant/actor authorization evidence. Runtime closure must bind the complete chain:
key ordering → tenant-scoped resolver → signed tuple → MDM reference read, with
zero reads on every rejected branch.

### F-04 — global behavior remains bounded in the candidate

The MDM route validates route/header tuple plus the custom service token before the
mediator. MDM `Program.cs` only registers the new options/validator. The normal
Platform MDM path retains caller bearer forwarding. No global MDM permission,
global `/api/internal` middleware rule or Carrier authorization relaxation was
observed in the 22-file candidate.

### F-05 — issuance/runtime coverage remains NOT RUN

Static wiring exists in login, refresh, MFA and forced-password handlers, but the
following are not runtime evidence: exactly-one, zero, multiple, inactive and
revoked scope; refusal/timeout; login; refresh re-resolution/no stale copy; MFA;
forced-password; wrong tuple/caller/audience/scope/expiry; and repository zero-read.
They remain `NOT RUN` until an authorized corrected writer handoff is frozen.

## 4. Acceptance disposition

| Area | Verdict | Boundary |
|---|---|---|
| Candidate byte integrity | PASS | Exact package only |
| Internal-key-before-binding ordering | PASS (static/E2) | Runtime reproduction still required |
| Narrow MDM route / no global expansion | PASS (static) | Exact 22-file candidate only |
| Exact service-token semantics | FAIL | F-01 and F-02 |
| Login/refresh/MFA/forced-password runtime | NOT RUN | No authorized writer-complete source |
| Real Auth-issued `legal_entity_id` | NOT RUN | No native process chain executed |
| Carrier E2E bounded handoff | NOT ISSUED | Requires corrected independent runtime PASS |

Test counts from the candidate writer are historical inputs and were not reported as
fresh independent execution. No test subset was aggregated into a broader PASS.

## 5. Native environment retained for successor VER

The existing native executable is `/Users/natig/.dotnet/dotnet`, version `8.0.417`,
SHA-256 `6d06c4a53676021a7572c989528067a3fe1195f569c27fbcac25e5302dc0d72c`.
No major roll-forward is needed. Reserved isolated endpoints remain Auth 5456,
Platform 5457, MDM 5459 and Mongo 37484 / `rsCarrierRealAuthVer01`; listeners must be
checked again immediately before any later run.

Because Phase 2 did not start, there is deliberately no source→binary→process→HTTP
chain in this report. Claiming one from candidate build evidence or the mutable
writer directory would violate the independent-verification boundary.

## 6. Exact close conditions

1. Owner authorization must bind the replacement patch/manifest that contains the
   F-01/F-02 correction; the unapproved candidate text is not authority.
2. The writer must publish writer-complete plus an immutable full build-source
   archive and final 22-target (or explicitly replaced) manifest.
3. A different verifier must fresh-build that immutable source and execute every
   row in `RUNTIME-ACCEPTANCE.tsv` in an isolated native .NET 8 environment.
4. Only a bounded PASS of the complete Auth→Platform→MDM→Auth-issued-token chain may
   be handed to Carrier E2E. It does not grant rollout, Supplier concurrence or
   E5/G5 acceptance.

## 7. Repository preservation

The pre-report dirty inventory contained 2,410 entries with SHA-256
`18fe1f92d68c17e310abdc70973c4784b6bdef7c237af169c91ab184a8729b28`.
This verifier added only this audit directory. No commit, push or stash occurred.

