package eu.grandmedical.diten.mobile.core.auth.data

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import eu.grandmedical.diten.mobile.core.auth.PlaintextTokenCipher
import eu.grandmedical.diten.mobile.core.auth.authApiFor
import eu.grandmedical.diten.mobile.core.auth.buildJwt
import eu.grandmedical.diten.mobile.core.auth.jwt.JwtDecoder
import eu.grandmedical.diten.mobile.core.auth.safeApiCaller
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import eu.grandmedical.diten.mobile.core.auth.testDataStore
import eu.grandmedical.diten.mobile.core.auth.testJson
import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import eu.grandmedical.diten.mobile.core.common.UiResult
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.runTest
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class AuthRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    private lateinit var server: MockWebServer
    private lateinit var scope: CoroutineScope
    private lateinit var dataStore: DataStore<Preferences>
    private lateinit var store: EncryptedTokenStore
    private lateinit var sessionManager: SessionManager
    private lateinit var repository: AuthRepository

    private val jwtDecoder = JwtDecoder()

    @Before
    fun setUp() {
        server = MockWebServer().apply { start() }
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        dataStore = testDataStore(tempFolder.newFolder(), scope)
        store = EncryptedTokenStore(dataStore, PlaintextTokenCipher())
        sessionManager = SessionManager(store, jwtDecoder)
        val json = testJson()
        repository = AuthRepositoryImpl(
            authApi = authApiFor(server, json),
            safeApiCaller = safeApiCaller(json),
            tokenStore = store,
            sessionManager = sessionManager,
            jwtDecoder = jwtDecoder,
        )
    }

    @After
    fun tearDown() {
        server.shutdown()
        scope.cancel()
    }

    @Test
    fun `login success persists tokens parses legal entities and authenticates`() = runTest {
        val accessToken = buildJwt(
            """{"sub":"u1","tenant_id":"tenant-1","legal_entities":"le-1,le-2,le-3","exp":$FAR_FUTURE}""",
        )
        server.enqueue(successEnvelope(accessToken = accessToken, refreshToken = "refresh-1"))

        val result = repository.login("gm@grandmedical.eu", "pw", "tenant-1")

        assertTrue(result is UiResult.Success)
        val payload = (result as UiResult.Success).data
        assertTrue(payload is LoginResult.Authenticated)

        // Persisted material.
        assertEquals(accessToken, store.accessToken())
        assertEquals("refresh-1", store.refreshToken())
        assertEquals(listOf("le-1", "le-2", "le-3"), store.availableLegalEntities())
        assertEquals("le-1", store.selectedLegalEntityId())

        // Session state.
        val state = sessionManager.authState.value
        assertTrue(state is AuthState.Authenticated)
        state as AuthState.Authenticated
        assertEquals("tenant-1", state.tenantId)
        assertEquals(listOf("le-1", "le-2", "le-3"), state.availableLegalEntities)
        assertEquals("le-1", state.selectedLegalEntityId)

        // X-Tenant-Id header was sent on the login request.
        assertEquals("tenant-1", server.takeRequest().getHeader("X-Tenant-Id"))
    }

    @Test
    fun `login failure envelope maps to UiResult Error`() = runTest {
        server.enqueue(
            MockResponse()
                .setResponseCode(400)
                .setBody("""{"data":null,"statusCode":400,"isSuccessful":false,"errors":["Bad credentials"]}"""),
        )

        val result = repository.login("gm@grandmedical.eu", "wrong", "tenant-1")

        assertTrue(result is UiResult.Error)
        assertEquals("Bad credentials", (result as UiResult.Error).message)
        assertTrue(sessionManager.authState.value is AuthState.Unauthenticated)
    }

    @Test
    fun `login requiring mfa returns MfaRequired`() = runTest {
        server.enqueue(
            MockResponse().setBody(
                """{"data":{"requiresMfa":true,"challengeId":"chal-1","channel":"sms",
                   "maskedDestination":"+90***45"},"statusCode":200,"isSuccessful":true,"errors":[]}""",
            ),
        )

        val result = repository.login("gm@grandmedical.eu", "pw", "tenant-1")

        assertTrue(result is UiResult.Success)
        val payload = (result as UiResult.Success).data
        assertTrue(payload is LoginResult.MfaRequired)
        payload as LoginResult.MfaRequired
        assertEquals("chal-1", payload.challengeId)
        assertEquals("sms", payload.channel)
        // No session yet.
        assertTrue(sessionManager.authState.value is AuthState.Unauthenticated)
    }

    @Test
    fun `refresh success updates stored access token`() = runTest {
        val oldAccess = buildJwt("""{"tenant_id":"tenant-1","legal_entities":"le-1","exp":$FAR_FUTURE}""")
        store.persistSession(oldAccess, "refresh-old", FAR_FUTURE, "tenant-1", listOf("le-1"), "le-1")

        val newAccess = buildJwt("""{"tenant_id":"tenant-1","legal_entities":"le-1","exp":$FAR_FUTURE}""")
        server.enqueue(successEnvelope(accessToken = newAccess, refreshToken = "refresh-new"))

        val refreshed = repository.refresh()

        assertTrue(refreshed)
        assertEquals(newAccess, store.accessToken())
        assertEquals("refresh-new", store.refreshToken())
    }

    @Test
    fun `refresh failure clears session and returns false`() = runTest {
        val oldAccess = buildJwt("""{"tenant_id":"tenant-1","legal_entities":"le-1","exp":$FAR_FUTURE}""")
        store.persistSession(oldAccess, "refresh-old", FAR_FUTURE, "tenant-1", listOf("le-1"), "le-1")
        sessionManager.restore()

        server.enqueue(MockResponse().setResponseCode(401).setBody("""{"statusCode":401,"isSuccessful":false,"errors":["Expired"]}"""))

        val refreshed = repository.refresh()

        assertFalse(refreshed)
        assertNull(store.accessToken())
        assertTrue(sessionManager.authState.value is AuthState.Unauthenticated)
    }

    private fun successEnvelope(accessToken: String, refreshToken: String): MockResponse =
        MockResponse().setBody(
            """{"data":{"accessToken":"$accessToken","refreshToken":"$refreshToken",
               "requiresMfa":false},"statusCode":200,"isSuccessful":true,"errors":[]}""",
        )

    private companion object {
        const val FAR_FUTURE = 4102444800L // 2100-01-01
    }
}
