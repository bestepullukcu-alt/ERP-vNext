package eu.grandmedical.diten.mobile.core.auth.session

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import dagger.Lazy
import eu.grandmedical.diten.mobile.core.auth.PlaintextTokenCipher
import eu.grandmedical.diten.mobile.core.auth.buildJwt
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.data.LoginResult
import eu.grandmedical.diten.mobile.core.auth.jwt.JwtDecoder
import eu.grandmedical.diten.mobile.core.auth.seam.SessionTokenProviderImpl
import eu.grandmedical.diten.mobile.core.auth.seam.TokenRefresherImpl
import eu.grandmedical.diten.mobile.core.auth.testDataStore
import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import eu.grandmedical.diten.mobile.core.common.UiResult
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class SessionAndSeamTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    private lateinit var scope: CoroutineScope
    private lateinit var dataStore: DataStore<Preferences>
    private lateinit var store: EncryptedTokenStore
    private lateinit var sessionManager: SessionManager

    private val jwtDecoder = JwtDecoder()

    @Before
    fun setUp() {
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        dataStore = testDataStore(tempFolder.newFolder(), scope)
        store = EncryptedTokenStore(dataStore, PlaintextTokenCipher())
        sessionManager = SessionManager(store, jwtDecoder)
    }

    @After
    fun tearDown() {
        scope.cancel()
    }

    private suspend fun seedAuthenticated() {
        val access = buildJwt("""{"tenant_id":"tenant-1","legal_entities":"le-1,le-2","exp":$FAR_FUTURE}""")
        store.persistSession(access, "r", FAR_FUTURE, "tenant-1", listOf("le-1", "le-2"), "le-1")
        sessionManager.restore()
    }

    @Test
    fun `selectLegalEntity persists a valid id and provider returns it`() = runTest {
        seedAuthenticated()

        val selected = sessionManager.selectLegalEntity("le-2")

        assertTrue(selected)
        assertEquals("le-2", store.selectedLegalEntityId())
        assertEquals("le-2", (sessionManager.authState.value as AuthState.Authenticated).selectedLegalEntityId)

        val provider = SessionTokenProviderImpl(store)
        assertEquals("le-2", provider.legalEntityId())
    }

    @Test
    fun `selectLegalEntity rejects an id not in the available set`() = runTest {
        seedAuthenticated()

        val selected = sessionManager.selectLegalEntity("le-999")

        assertFalse(selected)
        // Unchanged.
        assertEquals("le-1", store.selectedLegalEntityId())
        assertEquals("le-1", (sessionManager.authState.value as AuthState.Authenticated).selectedLegalEntityId)
    }

    @Test
    fun `session token provider returns stored access tenant and selected values`() = runTest {
        val access = buildJwt("""{"tenant_id":"tenant-1","legal_entities":"le-1,le-2","exp":$FAR_FUTURE}""")
        store.persistSession(access, "r", FAR_FUTURE, "tenant-1", listOf("le-1", "le-2"), "le-2")

        val provider = SessionTokenProviderImpl(store)

        assertEquals(access, provider.accessToken())
        assertEquals("tenant-1", provider.tenantId())
        assertEquals("le-2", provider.legalEntityId())
    }

    @Test
    fun `token refresher delegates to the repository`() = runTest {
        val fakeRepo = FakeAuthRepository(refreshResult = true)
        val refresher = TokenRefresherImpl(Lazy { fakeRepo })

        val result = refresher.refresh()

        assertTrue(result)
        assertEquals(1, fakeRepo.refreshCount)
    }

    @Test
    fun `restore leaves an expired token unauthenticated`() = runTest {
        val expired = buildJwt("""{"tenant_id":"t","legal_entities":"le-1","exp":1}""")
        store.persistSession(expired, "r", 1L, "t", listOf("le-1"), "le-1")

        sessionManager.restore()

        assertTrue(sessionManager.authState.value is AuthState.Unauthenticated)
    }

    private class FakeAuthRepository(private val refreshResult: Boolean) : AuthRepository {
        var refreshCount = 0
            private set

        override suspend fun login(
            email: String,
            password: String,
            tenantId: String,
            rememberMe: Boolean,
        ): UiResult<LoginResult> = UiResult.Error("unused")

        override suspend fun verifyMfa(challengeId: String, code: String): UiResult<LoginResult> =
            UiResult.Error("unused")

        override suspend fun refresh(): Boolean {
            refreshCount++
            return refreshResult
        }

        override suspend fun logout() = Unit
    }

    private companion object {
        const val FAR_FUTURE = 4102444800L
    }
}
