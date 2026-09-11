package eu.grandmedical.diten.mobile.core.sync

import javax.inject.Inject
import javax.inject.Singleton
import kotlin.coroutines.cancellation.CancellationException

/**
 * Runs every registered [SyncHandler] and aggregates their results.
 *
 * Handlers are supplied by Hilt as a multibound `Set` (`@IntoSet`). The engine is
 * intentionally robust: each handler runs in isolation, and one that throws or
 * fails is contained (recorded as a failure) without aborting the rest. The
 * aggregation rules are:
 *  - any [SyncResult.Retry] -> the overall pass is [SyncSummary.retryable];
 *  - any [SyncResult.Failure] (or thrown exception) -> recorded in
 *    [SyncSummary.failures];
 *  - all [SyncResult.Success] -> [SyncSummary.isSuccess].
 */
@Singleton
class SyncEngine @Inject constructor(
    private val handlers: Set<@JvmSuppressWildcards SyncHandler>,
) {

    suspend fun syncAll(): SyncSummary {
        var succeeded = 0
        var retryable = false
        val failures = mutableListOf<String>()

        for (handler in handlers) {
            when (val result = runHandler(handler)) {
                is SyncResult.Success -> succeeded++
                is SyncResult.Retry -> retryable = true
                is SyncResult.Failure -> failures += "${handler.key}: ${result.reason}"
            }
        }

        return SyncSummary(
            total = handlers.size,
            succeeded = succeeded,
            retryable = retryable,
            failures = failures,
        )
    }

    // A misbehaving handler must not take down the whole sync pass; translate any
    // thrown failure into a contained permanent failure for this handler only.
    @Suppress("TooGenericExceptionCaught")
    private suspend fun runHandler(handler: SyncHandler): SyncResult =
        try {
            handler.sync()
        } catch (cancellation: CancellationException) {
            // Never swallow structured-concurrency cancellation.
            throw cancellation
        } catch (throwable: Throwable) {
            SyncResult.Failure(throwable.message ?: throwable::class.java.simpleName)
        }
}
