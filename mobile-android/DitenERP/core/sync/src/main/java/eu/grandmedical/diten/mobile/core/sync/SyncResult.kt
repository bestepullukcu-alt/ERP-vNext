package eu.grandmedical.diten.mobile.core.sync

/**
 * Outcome of a single [SyncHandler.sync] pass.
 *
 * The distinction drives WorkManager's retry policy:
 *  - [Success] — the handler reconciled everything it owns.
 *  - [Retry] — a *transient* problem (connectivity, backend temporarily
 *    unavailable, wiring not yet complete); the worker should try again later.
 *  - [Failure] — a *permanent* problem the same inputs will keep hitting; a plain
 *    retry will not help, so it is recorded rather than retried forever.
 */
sealed interface SyncResult {

    /** The handler completed and its rows are reconciled with the server. */
    data object Success : SyncResult

    /** A transient failure; the worker should retry later. */
    data class Retry(val reason: String) : SyncResult

    /** A permanent failure; retrying with the same inputs will not help. */
    data class Failure(val reason: String) : SyncResult
}
