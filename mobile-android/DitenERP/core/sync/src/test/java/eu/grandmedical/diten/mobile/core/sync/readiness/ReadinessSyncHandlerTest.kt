package eu.grandmedical.diten.mobile.core.sync.readiness

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import eu.grandmedical.diten.mobile.core.database.DitenDatabase
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.dao.CachedReadinessDao
import eu.grandmedical.diten.mobile.core.database.entity.CachedReadinessEntity
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import java.util.Optional

/**
 * Proves the offline-first loop end to end against a REAL in-memory Room from
 * `:core:database`: seed PENDING rows -> run the handler with a fake sink ->
 * the sink receives exactly the pending rows AND Room reflects the reconciled
 * status (SYNCED on success; PENDING/FAILED, never SYNCED, on failure).
 */
@RunWith(RobolectricTestRunner::class)
class ReadinessSyncHandlerTest {

    private lateinit var db: DitenDatabase
    private lateinit var dao: CachedReadinessDao

    private val tenant = "tenant-A"
    private val legalEntity = "le-1"
    private val scope = ScopeKeys(tenantId = tenant, legalEntityId = legalEntity)

    /** Records what it was asked to push and returns a scripted verdict. */
    private class FakeSink(private val verdict: (CachedReadinessEntity) -> SyncOutcome) :
        ReadinessRemoteSink {
        val pushed = mutableListOf<CachedReadinessEntity>()
        override suspend fun push(row: CachedReadinessEntity): SyncOutcome {
            pushed += row
            return verdict(row)
        }
    }

    @Before
    fun setUp() {
        db = Room.inMemoryDatabaseBuilder(
            ApplicationProvider.getApplicationContext(),
            DitenDatabase::class.java,
        ).allowMainThreadQueries().build()
        dao = db.cachedReadinessDao()
    }

    @After
    fun tearDown() {
        db.close()
    }

    private fun row(id: String, status: SyncStatus) = CachedReadinessEntity(
        id = id,
        scope = scope,
        code = id,
        displayName = "Name $id",
        state = "READY",
        tags = listOf("cache"),
        updatedAt = 1_000L,
        syncStatus = status,
    )

    private fun handler(sink: FakeSink) = ReadinessSyncHandler(
        dao = dao,
        sink = Optional.of(sink),
        scopeProvider = Optional.of(ReadinessScopeProvider { listOf(scope) }),
    )

    @Test
    fun success_pushes_pending_rows_and_marks_them_synced() = runTest {
        dao.upsert(
            listOf(
                row("p1", SyncStatus.PENDING),
                row("p2", SyncStatus.PENDING),
                row("s1", SyncStatus.SYNCED),
            ),
        )
        val sink = FakeSink { SyncOutcome.Accepted }

        val result = handler(sink).sync()

        assertEquals(SyncResult.Success, result)
        // Only the two PENDING rows were pushed (the already-SYNCED row was skipped).
        assertEquals(setOf("p1", "p2"), sink.pushed.map { it.id }.toSet())
        // Room now reflects the reconciled state: all rows SYNCED.
        val stored = dao.getInScope(tenant, legalEntity).associateBy { it.id }
        assertEquals(SyncStatus.SYNCED, stored.getValue("p1").syncStatus)
        assertEquals(SyncStatus.SYNCED, stored.getValue("p2").syncStatus)
        assertEquals(SyncStatus.SYNCED, stored.getValue("s1").syncStatus)
    }

    @Test
    fun rejected_rows_become_failed_never_synced() = runTest {
        dao.upsert(row("p1", SyncStatus.PENDING))
        val sink = FakeSink { SyncOutcome.Rejected("validation") }

        val result = handler(sink).sync()

        assertEquals(SyncResult.Success, result)
        val stored = dao.getInScope(tenant, legalEntity).single()
        assertEquals(SyncStatus.FAILED, stored.syncStatus)
    }

    @Test
    fun unreachable_leaves_rows_pending_and_asks_to_retry() = runTest {
        dao.upsert(row("p1", SyncStatus.PENDING))
        val sink = FakeSink { SyncOutcome.Unreachable }

        val result = handler(sink).sync()

        assertTrue(result is SyncResult.Retry)
        val stored = dao.getInScope(tenant, legalEntity).single()
        assertEquals(SyncStatus.PENDING, stored.syncStatus)
    }

    @Test
    fun absent_sink_is_a_retry_and_touches_nothing() = runTest {
        dao.upsert(row("p1", SyncStatus.PENDING))
        val handler = ReadinessSyncHandler(
            dao = dao,
            sink = Optional.empty(),
            scopeProvider = Optional.of(ReadinessScopeProvider { listOf(scope) }),
        )

        val result = handler.sync()

        assertTrue(result is SyncResult.Retry)
        assertEquals(SyncStatus.PENDING, dao.getInScope(tenant, legalEntity).single().syncStatus)
    }
}
