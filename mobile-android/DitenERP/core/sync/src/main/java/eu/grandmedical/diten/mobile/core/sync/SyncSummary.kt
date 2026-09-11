package eu.grandmedical.diten.mobile.core.sync

/**
 * Aggregated outcome of running every [SyncHandler] in one [SyncEngine.syncAll] pass.
 *
 * @property total number of handlers that ran.
 * @property succeeded number that returned [SyncResult.Success].
 * @property retryable true if any handler asked to be retried (transient failure).
 * @property failures human-readable reasons for permanent failures (and contained
 *   exceptions), one per failing handler.
 */
data class SyncSummary(
    val total: Int,
    val succeeded: Int,
    val retryable: Boolean,
    val failures: List<String>,
) {
    /** True only when every handler succeeded — nothing to retry and nothing failed. */
    val isSuccess: Boolean
        get() = !retryable && failures.isEmpty() && succeeded == total
}
