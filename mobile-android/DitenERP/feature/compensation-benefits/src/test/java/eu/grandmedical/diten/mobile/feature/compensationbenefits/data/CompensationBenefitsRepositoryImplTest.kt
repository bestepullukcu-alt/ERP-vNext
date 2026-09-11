package eu.grandmedical.diten.mobile.feature.compensationbenefits.data

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.work.testing.WorkManagerTestInitHelper
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncScheduler
import eu.grandmedical.diten.mobile.feature.compensationbenefits.FakeCompensationBenefitsApi
import eu.grandmedical.diten.mobile.feature.compensationbenefits.FakeScopeProvider
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDao
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDatabase
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.mapper.toEntity
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.NewCompensationBenefits
import eu.grandmedical.diten.mobile.feature.compensationbenefits.listItemDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.readinessDto
import kotlinx.coroutines.flow.first
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
 * Proves the offline-first repository against a REAL in-memory
 * [CompensationBenefitsDatabase] plus a fake [FakeCompensationBenefitsApi]:
 *  - `create` writes a PENDING row observable via `observeList` WITHOUT any
 *    server call;
 *  - `refresh` upserts server rows as SYNCED, scoped to tenant + legal entity;
 *  - scoping excludes other tenants/legal entities (isolation);
 *  - `evaluate` updates the row; `delete` removes it.
 */
@RunWith(RobolectricTestRunner::class)
class CompensationBenefitsRepositoryImplTest {

    private lateinit var db: CompensationBenefitsDatabase
    private lateinit var dao: CompensationBenefitsDao
    private lateinit var api: FakeCompensationBenefitsApi
    private lateinit var scheduler: SyncScheduler

    private val scope = ScopeKeys(tenantId = "tenant-A", legalEntityId = "le-1")
    private val otherScope = ScopeKeys(tenantId = "tenant-B", legalEntityId = "le-9")

    private val safeApiCaller = SafeApiCaller(Json { ignoreUnknownKeys = true; explicitNulls = false })

    @Before
    fun setUp() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        WorkManagerTestInitHelper.initializeTestWorkManager(context)
        db = Room.inMemoryDatabaseBuilder(context, CompensationBenefitsDatabase::class.java)
            .allowMainThreadQueries()
            .build()
        dao = db.compensationBenefitsDao()
        api = FakeCompensationBenefitsApi()
        scheduler = SyncScheduler(context)
    }

    @After
    fun tearDown() {
        db.close()
    }

    private fun repository(activeScope: ScopeKeys? = scope) = CompensationBenefitsRepositoryImpl(
        dao = dao,
        api = api,
        safeApiCaller = safeApiCaller,
        scopeProvider = FakeScopeProvider(activeScope),
        syncScheduler = scheduler,
    )

    @Test
    fun create_writesPendingRow_observableWithoutAnyServerCall() = runTest {
        val repo = repository()

        val result = repo.create(NewCompensationBenefits(code = "CB-1", displayName = "Local only"))

        assertTrue(result is UiResult.Success)
        // Offline-first: NO network call was made.
        assertEquals(0, api.createCount)
        // The row is visible via observeList and marked PENDING.
        val list = repo.observeList().first()
        assertEquals(1, list.size)
        assertEquals("Local only", list.single().displayName)
        assertEquals(SyncStatus.PENDING, list.single().syncStatus)
    }

    @Test
    fun refresh_upsertsServerRowsAsSynced_inScope() = runTest {
        api.onList = { FakeCompensationBenefitsApi.ok(listOf(listItemDto("s1"), listItemDto("s2"))) }
        val repo = repository()

        val result = repo.refresh()

        assertTrue(result is UiResult.Success)
        assertEquals(1, api.listCount)
        val stored = dao.getInScope(scope.tenantId, scope.legalEntityId)
        assertEquals(setOf("s1", "s2"), stored.map { it.id }.toSet())
        assertTrue(stored.all { it.syncStatus == SyncStatus.SYNCED })
        assertTrue(stored.all { it.scope == scope })
    }

    @Test
    fun observeList_excludesOtherTenantAndLegalEntity() = runTest {
        // Seed a row in a DIFFERENT scope directly.
        dao.upsert(readinessDto("other").toEntity(otherScope))
        val repo = repository()

        // Add one in the active scope.
        repo.create(NewCompensationBenefits(code = "CB-mine", displayName = "Mine"))

        val list = repo.observeList().first()
        assertEquals(listOf("Mine"), list.map { it.displayName })
    }

    @Test
    fun evaluate_updatesTheRow() = runTest {
        dao.upsert(readinessDto("id-1", compensationBenefitsReadinessState = 0).toEntity(scope)) // Draft
        // Evaluate returns Ready (=1 in THIS module).
        api.onEvaluate = { FakeCompensationBenefitsApi.ok(readinessDto("id-1", compensationBenefitsReadinessState = 1)) }
        val repo = repository()

        val result = repo.evaluate("id-1")

        assertTrue(result is UiResult.Success)
        assertEquals(1, dao.getById("id-1")?.compensationBenefitsReadinessState)
    }

    @Test
    fun delete_syncedRow_callsBackend_thenRemovesTheRow() = runTest {
        dao.upsert(readinessDto("id-1").toEntity(scope)) // SYNCED
        val repo = repository()

        val result = repo.delete("id-1")

        assertTrue(result is UiResult.Success)
        assertEquals(1, api.deleteCount)
        assertNull(dao.getById("id-1"))
    }

    @Test
    fun delete_neverSyncedRow_removesLocally_withNoServerCall() = runTest {
        // A PENDING row carries a LOCAL id the backend never saw.
        dao.upsert(readinessDto("local-1").toEntity(scope).copy(syncStatus = SyncStatus.PENDING))
        val repo = repository()

        val result = repo.delete("local-1")

        assertTrue(result is UiResult.Success)
        assertEquals(0, api.deleteCount)
        assertNull(dao.getById("local-1"))
    }

    @Test
    fun delete_failedRow_removesLocally_withNoServerCall() = runTest {
        dao.upsert(readinessDto("local-2").toEntity(scope).copy(syncStatus = SyncStatus.FAILED))
        val repo = repository()

        val result = repo.delete("local-2")

        assertTrue(result is UiResult.Success)
        assertEquals(0, api.deleteCount)
        assertNull(dao.getById("local-2"))
    }

    @Test
    fun delete_syncedRow_serverReturns404_stillRemovesLocalRow() = runTest {
        dao.upsert(readinessDto("id-1").toEntity(scope)) // SYNCED
        api.onDelete = { FakeCompensationBenefitsApi.error(404) }
        val repo = repository()

        val result = repo.delete("id-1")

        // Already gone server-side is reconciled to success.
        assertTrue(result is UiResult.Success)
        assertEquals(1, api.deleteCount)
        assertNull(dao.getById("id-1"))
    }
}
