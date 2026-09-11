package eu.grandmedical.diten.mobile.core.sync.readiness

import eu.grandmedical.diten.mobile.core.database.dao.CachedReadinessDao
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.sync.SyncHandler
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import java.util.Optional
import javax.inject.Inject

/**
 * Reference [SyncHandler] that demonstrates the full offline-first loop against
 * the real `:core:database` (Room as the Single Source Of Truth):
 *
 * 1. **pull pending** — read the scope's rows and keep those marked
 *    [SyncStatus.PENDING] (locally created/updated, not yet confirmed);
 * 2. **push** — send each pending row through [ReadinessRemoteSink];
 * 3. **reconcile** — apply the server verdict back into Room: [SyncOutcome.Accepted]
 *    -> [SyncStatus.SYNCED], [SyncOutcome.Rejected] -> [SyncStatus.FAILED],
 *    [SyncOutcome.Unreachable] -> leave [SyncStatus.PENDING] and ask to retry.
 *
 * Both collaborators are optional at this stage: the real remote sink and the
 * active-scope provider are deferred to the feature/app layer (`@BindsOptionalOf`
 * in `SyncBindingsModule`). While either is absent this handler returns
 * [SyncResult.Retry] instead of fabricating a backend call. Real per-feature
 * handlers (M1+) will follow this exact shape with their own DAO + sink.
 */
class ReadinessSyncHandler @Inject constructor(
    private val dao: CachedReadinessDao,
    private val sink: Optional<ReadinessRemoteSink>,
    private val scopeProvider: Optional<ReadinessScopeProvider>,
) : SyncHandler {

    override val key: String = KEY

    @Suppress("ReturnCount")
    override suspend fun sync(): SyncResult {
        val remote = sink.orElse(null)
            ?: return SyncResult.Retry("ReadinessRemoteSink not wired yet (deferred to feature module)")
        val provider = scopeProvider.orElse(null)
            ?: return SyncResult.Retry("ReadinessScopeProvider not wired yet (deferred to feature module)")

        for (scope in provider.scopesToSync()) {
            val pending = dao.getInScope(scope.tenantId, scope.legalEntityId)
                .filter { it.syncStatus == SyncStatus.PENDING }

            for (row in pending) {
                when (val outcome = remote.push(row)) {
                    SyncOutcome.Accepted ->
                        dao.upsert(row.copy(syncStatus = SyncStatus.SYNCED))
                    is SyncOutcome.Rejected ->
                        dao.upsert(row.copy(syncStatus = SyncStatus.FAILED))
                    SyncOutcome.Unreachable ->
                        return SyncResult.Retry("connectivity pushing readiness row ${row.id}: $outcome")
                }
            }
        }

        return SyncResult.Success
    }

    companion object {
        const val KEY = "readiness"
    }
}
