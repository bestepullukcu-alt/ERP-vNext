package eu.grandmedical.diten.mobile.core.auth.token

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import eu.grandmedical.diten.mobile.core.auth.PlaintextTokenCipher
import eu.grandmedical.diten.mobile.core.auth.testDataStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class EncryptedTokenStoreTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    private lateinit var scope: CoroutineScope
    private lateinit var dataStore: DataStore<Preferences>
    private lateinit var store: EncryptedTokenStore

    @Before
    fun setUp() {
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        dataStore = testDataStore(tempFolder.newFolder(), scope)
        store = EncryptedTokenStore(dataStore, PlaintextTokenCipher())
    }

    @After
    fun tearDown() {
        scope.cancel()
    }

    @Test
    fun `round-trips tokens and scope`() = runTest {
        store.persistSession(
            accessToken = "access-1",
            refreshToken = "refresh-1",
            expiresAt = 123456789L,
            tenantId = "tenant-1",
            availableLegalEntities = listOf("le-1", "le-2", "le-3"),
            selectedLegalEntityId = "le-2",
        )

        assertEquals("access-1", store.accessToken())
        assertEquals("refresh-1", store.refreshToken())
        assertEquals(123456789L, store.expiresAt())
        assertEquals("tenant-1", store.tenantId())
        assertEquals("le-2", store.selectedLegalEntityId())
        assertEquals(listOf("le-1", "le-2", "le-3"), store.availableLegalEntities())
    }

    @Test
    fun `updateTokens rotates only the token material`() = runTest {
        store.persistSession("a0", "r0", 1L, "tenant-1", listOf("le-1"), "le-1")

        store.updateTokens("a1", "r1", 999L)

        assertEquals("a1", store.accessToken())
        assertEquals("r1", store.refreshToken())
        assertEquals(999L, store.expiresAt())
        // Scope is untouched by a refresh.
        assertEquals("tenant-1", store.tenantId())
        assertEquals("le-1", store.selectedLegalEntityId())
    }

    @Test
    fun `clear wipes everything`() = runTest {
        store.persistSession("a", "r", 1L, "t", listOf("le-1"), "le-1")

        store.clear()

        assertNull(store.accessToken())
        assertNull(store.refreshToken())
        assertNull(store.expiresAt())
        assertNull(store.tenantId())
        assertNull(store.selectedLegalEntityId())
        assertEquals(emptyList<String>(), store.availableLegalEntities())
    }

    @Test
    fun `fresh store reads empty safely`() = runTest {
        assertNull(store.accessToken())
        assertNull(store.expiresAt())
        assertEquals(emptyList<String>(), store.availableLegalEntities())
    }
}
