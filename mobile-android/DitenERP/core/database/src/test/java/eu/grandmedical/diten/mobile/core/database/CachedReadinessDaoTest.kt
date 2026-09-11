package eu.grandmedical.diten.mobile.core.database

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import eu.grandmedical.diten.mobile.core.database.dao.CachedReadinessDao
import eu.grandmedical.diten.mobile.core.database.entity.CachedReadinessEntity
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

/**
 * Proves the tenant + legal-entity partition actually isolates data — not just
 * that the queries compile. Rows for three (tenant, legal entity) scopes are
 * seeded and every read is checked to return exactly its scope.
 */
@RunWith(RobolectricTestRunner::class)
class CachedReadinessDaoTest {

    private lateinit var db: DitenDatabase
    private lateinit var dao: CachedReadinessDao

    private val tenantA = "tenant-A"
    private val tenantB = "tenant-B"
    private val entity1 = "le-1"
    private val entity2 = "le-2"

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

    private fun row(
        id: String,
        tenantId: String,
        legalEntityId: String,
        code: String = id,
        state: String = "READY",
    ) = CachedReadinessEntity(
        id = id,
        scope = ScopeKeys(tenantId = tenantId, legalEntityId = legalEntityId),
        code = code,
        displayName = "Name $id",
        state = state,
        // Fixed non-empty tags so the List<String> converter is exercised on write.
        tags = listOf("cache", "readiness"),
        updatedAt = 1_000L,
        syncStatus = SyncStatus.SYNCED,
    )

    private suspend fun seedThreeScopes() {
        dao.upsert(
            listOf(
                row("a1", tenantA, entity1),
                row("a2", tenantA, entity2),
                row("b1", tenantB, entity1),
            ),
        )
    }

    @Test
    fun writeScopeQuery_returnsOnlyThatScope() = runTest {
        seedThreeScopes()

        val result = dao.getInScope(tenantA, entity1)

        assertEquals(1, result.size)
        assertEquals("a1", result.single().id)
        // Isolation: tenantB's row sharing legal_entity_id "le-1" must NOT leak.
        assertTrue(result.none { it.scope.tenantId == tenantB })
    }

    @Test
    fun rollupQuery_returnsAllDescendantScopesButNotOtherTenant() = runTest {
        seedThreeScopes()

        val result = dao.getInRollup(tenantA, listOf(entity1, entity2))

        assertEquals(setOf("a1", "a2"), result.map { it.id }.toSet())
        // Roll-up stays inside the tenant: tenantB is excluded even though it
        // shares legal entity "le-1".
        assertTrue(result.all { it.scope.tenantId == tenantA })
        assertTrue(result.none { it.id == "b1" })
    }

    @Test
    fun upsert_replacesRowWithSamePrimaryKey() = runTest {
        dao.upsert(row("a1", tenantA, entity1, state = "READY"))
        dao.upsert(row("a1", tenantA, entity1, state = "BLOCKED"))

        val result = dao.getInScope(tenantA, entity1)

        assertEquals(1, result.size)
        assertEquals("BLOCKED", result.single().state)
    }

    @Test
    fun deleteScope_removesOnlyThatScope() = runTest {
        seedThreeScopes()

        dao.deleteScope(tenantA, entity1)

        assertTrue(dao.getInScope(tenantA, entity1).isEmpty())
        // Sibling and other-tenant scopes are untouched.
        assertEquals(1, dao.getInScope(tenantA, entity2).size)
        assertEquals(1, dao.getInScope(tenantB, entity1).size)
    }

    @Test
    fun observeInScope_emitsUpdatedListOnWrite() = runTest {
        dao.upsert(row("a1", tenantA, entity1))

        val initial = dao.observeInScope(tenantA, entity1).first()
        assertEquals(listOf("a1"), initial.map { it.id })

        dao.upsert(row("a3", tenantA, entity1))
        val afterInsert = dao.observeInScope(tenantA, entity1).first()
        assertEquals(setOf("a1", "a3"), afterInsert.map { it.id }.toSet())
    }
}
