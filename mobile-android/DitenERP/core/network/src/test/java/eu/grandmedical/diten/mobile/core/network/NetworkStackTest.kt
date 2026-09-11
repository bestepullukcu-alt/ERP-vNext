package eu.grandmedical.diten.mobile.core.network

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.api.AuthApi
import eu.grandmedical.diten.mobile.core.network.dto.LoginRequest
import eu.grandmedical.diten.mobile.core.network.interceptor.AuthAuthenticator
import eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * Deterministic MockWebServer coverage of the transport layer:
 * interceptors, envelope/HTTP mapping and the 401 refresh-and-retry flow.
 */
@Suppress("TooManyFunctions")
class NetworkStackTest {

    private lateinit var server: MockWebServer
    private val json = Json {
        ignoreUnknownKeys = true
        explicitNulls = false
    }
    private val caller = SafeApiCaller(json)

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        runCatching { server.shutdown() }
    }

    private fun retrofit(
        provider: SessionTokenProvider,
        refresher: TokenRefresher,
    ): Retrofit {
        val client = OkHttpClient.Builder()
            .addInterceptor(HeaderInterceptor(provider))
            .authenticator(AuthAuthenticator(refresher, provider))
            .build()
        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
    }

    private fun testApi(
        provider: SessionTokenProvider,
        refresher: TokenRefresher = FakeTokenRefresher(),
    ): TestApi = retrofit(provider, refresher).create(TestApi::class.java)

    private fun success(body: String) =
        MockResponse().setResponseCode(HTTP_OK).setBody(body).addHeader("Content-Type", "application/json")

    // --- Case 1a: authenticated request attaches all three headers -------------
    @Test
    fun headerInterceptorAttachesAllThreeHeaders() {
        val provider = FakeSessionTokenProvider(token = "tok", tenant = "tenant-1", legal = "legal-9")
        server.enqueue(success(SUCCESS_THING))

        runBlocking { testApi(provider).secure() }
        val request = server.takeRequest()

        assertEquals("Bearer tok", request.getHeader(NetworkHeaders.AUTHORIZATION))
        assertEquals("tenant-1", request.getHeader(NetworkHeaders.TENANT_ID))
        assertEquals("legal-9", request.getHeader(NetworkHeaders.LEGAL_ENTITY_ID))
        assertEquals("application/json", request.getHeader(NetworkHeaders.ACCEPT))
        // Internal marker must never reach the wire.
        assertNull(request.getHeader(NetworkHeaders.AUTH_MARKER))
    }

    // --- Case 1b: null provider values omit the headers ------------------------
    @Test
    fun headerInterceptorOmitsHeadersWhenNull() {
        val provider = FakeSessionTokenProvider(token = null, tenant = null, legal = null)
        server.enqueue(success(SUCCESS_THING))

        runBlocking { testApi(provider).secure() }
        val request = server.takeRequest()

        assertNull(request.getHeader(NetworkHeaders.AUTHORIZATION))
        assertNull(request.getHeader(NetworkHeaders.TENANT_ID))
        assertNull(request.getHeader(NetworkHeaders.LEGAL_ENTITY_ID))
        assertEquals("application/json", request.getHeader(NetworkHeaders.ACCEPT))
    }

    // --- Case 2: login endpoint carries no Authorization header ----------------
    @Test
    fun loginRequestHasNoAuthorizationHeader() {
        val provider = FakeSessionTokenProvider(token = "tok", tenant = "tenant-1", legal = "legal-9")
        val authApi = retrofit(provider, FakeTokenRefresher()).create(AuthApi::class.java)
        server.enqueue(success(SUCCESS_AUTH))

        runBlocking { authApi.login("tenant-1", LoginRequest("a@b.co", "pw", rememberMe = true)) }
        val request = server.takeRequest()

        assertNull(request.getHeader(NetworkHeaders.AUTHORIZATION))
        assertEquals("tenant-1", request.getHeader(NetworkHeaders.TENANT_ID))
        assertEquals("/api/tenant-auth/login", request.path)
    }

    // --- Case 3: successful envelope -> UiResult.Success -----------------------
    @Test
    fun successfulEnvelopeMapsToUiResultSuccess() {
        val provider = FakeSessionTokenProvider()
        server.enqueue(success(SUCCESS_THING))

        val result = runBlocking { caller.safeApiCall { testApi(provider).open() } }

        assertTrue(result is UiResult.Success)
        assertEquals(Thing("42", "widget"), (result as UiResult.Success).data)
    }

    // --- Case 4a: envelope isSuccessful=false -> Validation --------------------
    @Test
    fun failedEnvelopeMapsToValidation() {
        val provider = FakeSessionTokenProvider()
        server.enqueue(success(FAILED_ENVELOPE))

        val result = runBlocking { caller.apiCall { testApi(provider).open() } }

        assertTrue(result is ApiResponse.Failure)
        val error = (result as ApiResponse.Failure).error
        assertTrue(error is NetworkError.Validation)
        assertEquals("Name is required", (error as NetworkError.Validation).messages.first())
        assertEquals("Name is required", error.message)
    }

    // --- Case 4b: HTTP 400 -> Validation / UiResult.Error ----------------------
    @Test
    fun http400MapsToValidationError() {
        val provider = FakeSessionTokenProvider()
        server.enqueue(MockResponse().setResponseCode(HTTP_BAD_REQUEST).setBody(BAD_REQUEST_ENVELOPE))

        val rich = runBlocking { caller.apiCall { testApi(provider).open() } }
        server.enqueue(MockResponse().setResponseCode(HTTP_BAD_REQUEST).setBody(BAD_REQUEST_ENVELOPE))
        val ui = runBlocking { caller.safeApiCall { testApi(provider).open() } }

        assertTrue((rich as ApiResponse.Failure).error is NetworkError.Validation)
        assertTrue(ui is UiResult.Error)
        assertEquals("Bad field", (ui as UiResult.Error).message)
    }

    // --- Case 5a: HTTP 401 (refresh unavailable) -> Unauthorized ---------------
    @Test
    fun http401MapsToUnauthorized() {
        val provider = FakeSessionTokenProvider(token = "tok")
        val refresher = FakeTokenRefresher(result = false)
        server.enqueue(MockResponse().setResponseCode(HTTP_UNAUTHORIZED))

        val result = runBlocking { caller.apiCall { testApi(provider, refresher).secure() } }

        assertTrue((result as ApiResponse.Failure).error is NetworkError.Unauthorized)
    }

    // --- Case 5b: HTTP 403 -> Forbidden ----------------------------------------
    @Test
    fun http403MapsToForbidden() {
        val provider = FakeSessionTokenProvider()
        server.enqueue(MockResponse().setResponseCode(HTTP_FORBIDDEN))

        val result = runBlocking { caller.apiCall { testApi(provider).open() } }

        assertTrue((result as ApiResponse.Failure).error is NetworkError.Forbidden)
    }

    // --- Case 6: 401 triggers refresh then a single retry with the new token ---
    @Test
    fun http401TriggersRefreshAndRetriesOnce() {
        val provider = FakeSessionTokenProvider(token = "old-token", tenant = "t", legal = "l")
        val refresher = FakeTokenRefresher(result = true, onRefresh = { provider.token = "new-token" })
        server.enqueue(MockResponse().setResponseCode(HTTP_UNAUTHORIZED))
        server.enqueue(success(SUCCESS_THING))

        val result = runBlocking { caller.apiCall { testApi(provider, refresher).secure() } }

        assertTrue(result is ApiResponse.Success)
        assertEquals(2, server.requestCount)
        assertEquals(1, refresher.count)
        val first = server.takeRequest()
        val second = server.takeRequest()
        assertEquals("Bearer old-token", first.getHeader(NetworkHeaders.AUTHORIZATION))
        assertEquals("Bearer new-token", second.getHeader(NetworkHeaders.AUTHORIZATION))
    }

    // --- Case 7: IOException / timeout -> Connectivity -------------------------
    @Test
    fun ioFailureMapsToConnectivity() {
        val provider = FakeSessionTokenProvider()
        val api = testApi(provider)
        // Closing the server makes the next call fail with an IOException.
        server.shutdown()

        val result = runBlocking { caller.apiCall { api.open() } }

        assertTrue((result as ApiResponse.Failure).error is NetworkError.Connectivity)
    }

    private companion object {
        const val HTTP_OK = 200
        const val HTTP_BAD_REQUEST = 400
        const val HTTP_UNAUTHORIZED = 401
        const val HTTP_FORBIDDEN = 403

        const val SUCCESS_THING =
            """{"data":{"id":"42","name":"widget"},"statusCode":200,"isSuccessful":true,"errors":[]}"""
        const val SUCCESS_AUTH =
            """{"data":{"accessToken":"a","refreshToken":"r","requiresMfa":false},"statusCode":200,""" +
                """"isSuccessful":true,"errors":[]}"""
        const val FAILED_ENVELOPE =
            """{"data":null,"statusCode":200,"isSuccessful":false,"errors":["Name is required","Bad email"]}"""
        const val BAD_REQUEST_ENVELOPE =
            """{"data":null,"statusCode":400,"isSuccessful":false,"errors":["Bad field"]}"""
    }
}
