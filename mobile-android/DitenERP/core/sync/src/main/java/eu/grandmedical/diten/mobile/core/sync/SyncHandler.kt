package eu.grandmedical.diten.mobile.core.sync

/**
 * One feature's contribution to the offline-first sync pass.
 *
 * Each feature module implements exactly one handler and contributes it to the
 * Hilt multibinding `Set<SyncHandler>` (via `@IntoSet`). A handler owns the full
 * loop for its own data: read its `PENDING` rows from its DAO, push them to the
 * backend, then reconcile the server-authoritative result back into Room
 * (marking rows `SYNCED` or `FAILED`).
 *
 * Handlers must be self-contained and side-effect-isolated: a throwing or failing
 * handler must never prevent the others from running (see [SyncEngine]).
 */
interface SyncHandler {

    /** Stable identifier, useful for logging/diagnostics and summary reporting. */
    val key: String

    /** Runs this feature's pull-pending -> push -> reconcile loop. */
    suspend fun sync(): SyncResult
}
