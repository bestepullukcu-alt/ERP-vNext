package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.sync

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import eu.grandmedical.diten.mobile.feature.employeeonboarding.FakeEmployeeOnboardingApi
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingDao
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingDatabase
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper.toEntity
import eu.grandmedical.diten.mobile.feature.employeeonboarding.readinessDto
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
class EmployeeOnboardingSyncHandlerTest {

    private lateinit var db: EmployeeOnboardingDatabase
    private lateinit var dao: EmployeeOnboardingDao
    private lateinit var api: FakeEmployeeOnboardingApi

    private val scope = ScopeKeys(tenantId = "tenant-A", legalEntityId = "le-1")
    private val safeApiCaller = SafeApiCaller(Json { ignoreUnknownKeys = true; explicitNulls = false })

    @Before
    fun setUp() {
        db = Room.inMemoryDatabaseBuilder(
            ApplicationProvider.getApplicationContext(),
            EmployeeOnboardingDatabase::class.java,
        ).allowMainThreadQueries().build()
        dao = db.employeeOnboardingDao()
        api = FakeEmployeeOnboardingApi()
    }

    @After
    fun tearDown() {
        db.close()
    }

    private fun handler() = EmployeeOnboardingSyncHandler(dao = dao, api = api, safeApiCaller = safeApiCaller)

    private suspend fun seedPending(id: String) {
        dao.upsert(readinessDto(id).toEntity(scope).copy(syncStatus = SyncStatus.PENDING))
    }

    @Test
    fun pending_isPushed_thenMarkedSynced_withServerId() = runTest {
        seedPending("local-1")
        api.onCreate = { FakeEmployeeOnboardingApi.ok("server-1") }

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
        api.onCreate = { FakeEmployeeOnboardingApi.error(503) }

        val result = handler().sync()

        assertTrue(result is SyncResult.Retry)
        assertEquals(SyncStatus.PENDING, dao.getById("local-1")?.syncStatus)
    }

    @Test
    fun permanentValidationError_fails_andMarksRowFailed_neverSynced() = runTest {
        seedPending("local-1")
        api.onCreate = { FakeEmployeeOnboardingApi.error(400) }

        val result = handler().sync()

        assertTrue(result is SyncResult.Failure)
        assertEquals(SyncStatus.FAILED, dao.getById("local-1")?.syncStatus)
    }
}
