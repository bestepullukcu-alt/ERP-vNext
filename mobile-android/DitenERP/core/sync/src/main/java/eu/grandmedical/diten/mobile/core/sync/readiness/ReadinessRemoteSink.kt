package eu.grandmedical.diten.mobile.core.sync.readiness

import eu.grandmedical.diten.mobile.core.database.entity.CachedReadinessEntity

/**
 * The push side of the readiness sync loop.
 *
 * This is intentionally a bare interface: no generic readiness endpoint exists in
 * `:core:network` yet, so the REAL implementation is deferred to the feature
 * module that owns readiness (M1+). Until then the binding is left unfulfilled
 * (`@BindsOptionalOf`, see `SyncBindingsModule`) and [ReadinessSyncHandler] returns
 * [eu.grandmedical.diten.mobile.core.sync.SyncResult.Retry] rather than inventing a
 * fake backend call. Tests supply a fake sink to prove the loop end to end.
 */
interface ReadinessRemoteSink {

    /** Pushes one pending row to the backend and reports the server's verdict. */
    suspend fun push(row: CachedReadinessEntity): SyncOutcome
}

/**
 * Provides the set of scopes (`tenantId` + `legalEntityId`) whose pending rows a
 * sync pass should reconcile.
 *
 * Also deferred to the feature/app layer (which knows the active tenant and the
 * user's legal-entity assignments); left unfulfilled here via `@BindsOptionalOf`.
 */
fun interface ReadinessScopeProvider {

    /** The scopes to reconcile on this pass; empty means "nothing to sync". */
    suspend fun scopesToSync(): List<eu.grandmedical.diten.mobile.core.database.ScopeKeys>
}

/**
 * The server's verdict for a single pushed row.
 *  - [Accepted] — the server took the write; the row is now authoritative -> `SYNCED`.
 *  - [Rejected] — the server permanently refused it (validation/conflict) -> `FAILED`.
 *  - [Unreachable] — transient/connectivity; leave the row `PENDING` and retry.
 */
sealed interface SyncOutcome {
    data object Accepted : SyncOutcome
    data class Rejected(val reason: String) : SyncOutcome
    data object Unreachable : SyncOutcome
}
