package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.sync

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import eu.grandmedical.diten.mobile.feature.compensationbenefits.FakeCompensationBenefitsApi
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDao
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDatabase
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.mapper.toEntity
import eu.grandmedical.diten.mobile.feature.compensationbenefits.readinessDto
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

/**
 * Proves the feature's real sync handler (the @IntoSet contribution to the
 * app-wide Set<SyncHandler>) against real in-memory Room + a fake Api:
 *  - PENDING -> push -> SYNCED (server id stamped);
 *  - a transient (5xx) error -> Retry, row stays PENDING (never SYNCED);
 *  - a permanent (validation) error -> Failure, row FAILED (never SYNCED).
 */
@RunWith(RobolectricTestRunner::class)
class CompensationBenefitsSyncHandlerTest {

    private lateinit var db: CompensationBenefitsDatabase
    private lateinit var dao: CompensationBenefitsDao
    private lateinit var api: FakeCompensationBenefitsApi

    private val scope = ScopeKeys(tenantId = "tenant-A", legalEntityId = "le-1")
    private val safeApiCaller = SafeApiCaller(Json { ignoreUnknownKeys = true; explicitNulls = false })

    @Before
    fun setUp() {
        db = Room.inMemoryDatabaseBuilder(
            ApplicationProvider.getApplicationContext(),
            CompensationBenefitsDatabase::class.java,
        ).allowMainThreadQueries().build()
        dao = db.compensationBenefitsDao()
        api = FakeCompensationBenefitsApi()
    }

    @After
    fun tearDown() {
        db.close()
    }

    private fun handler() = CompensationBenefitsSyncHandler(dao = dao, api = api, safeApiCaller = safeApiCaller)

    private suspend fun seedPending(id: String) {
        dao.upsert(readinessDto(id).toEntity(scope).copy(syncStatus = SyncStatus.PENDING))
    }

    private suspend fun seedFailed(id: String) {
        dao.upsert(readinessDto(id).toEntity(scope).copy(syncStatus = SyncStatus.FAILED))
    }

    @Test
    fun pending_isPushed_thenMarkedSynced_withServerId() = runTest {
        seedPending("local-1")
        api.onCreate = { FakeCompensationBenefitsApi.ok("server-1") }

        val result = handler().sync()

        assertEquals(SyncResult.Success, result)
        assertEquals(1, api.createCount)
        // Temporary local-id row replaced by the server-id row, marked SYNCED.
        assertNull(dao.getById("local-1"))
        assertEquals(SyncStatus.SYNCED, dao.getById("server-1")?.syncStatus)
        assertTrue(dao.getByStatus(SyncStatus.PENDING).isEmpty())
    }

    @Test
    fun transientServerError_retries_andLeavesRowPending() = runTest {
        seedPending("local-1")
        api.onCreate = { FakeCompensationBenefitsApi.error(503) }

        val result = handler().sync()

        assertTrue(result is SyncResult.Retry)
        assertEquals(SyncStatus.PENDING, dao.getById("local-1")?.syncStatus)
    }

    @Test
    fun failedRow_isRetried_thenMarkedSynced_onSuccess() = runTest {
        // A row left FAILED by an earlier transient error must be retried.
        seedFailed("local-1")
        api.onCreate = { FakeCompensationBenefitsApi.ok("server-1") }

        val result = handler().sync()

        assertEquals(SyncResult.Success, result)
        assertEquals(1, api.createCount)
        assertNull(dao.getById("local-1"))
        assertEquals(SyncStatus.SYNCED, dao.getById("server-1")?.syncStatus)
    }

    @Test
    fun unauthorizedError_retries_andLeavesRowUnchanged() = runTest {
        // 401 during background sync = "token expired, retry after refresh".
        seedPending("local-1")
        api.onCreate = { FakeCompensationBenefitsApi.error(401) }

        val result = handler().sync()

        assertTrue(result is SyncResult.Retry)
        assertEquals(SyncStatus.PENDING, dao.getById("local-1")?.syncStatus)
    }

    @Test
    fun permanentValidationError_fails_andMarksRowFailed_neverSynced() = runTest {
        seedPending("local-1")
        api.onCreate = { FakeCompensationBenefitsApi.error(400) }

        val result = handler().sync()

        assertTrue(result is SyncResult.Failure)
        assertEquals(SyncStatus.FAILED, dao.getById("local-1")?.syncStatus)
    }
}
