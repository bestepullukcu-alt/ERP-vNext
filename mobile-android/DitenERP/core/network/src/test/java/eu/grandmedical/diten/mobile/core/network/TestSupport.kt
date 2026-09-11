package eu.grandmedical.diten.mobile.core.network

import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Headers

/** Minimal payload used to prove envelope deserialization. */
@Serializable
data class Thing(val id: String, val name: String)

/** Test Retrofit surface: one authenticated endpoint, one open endpoint. */
interface TestApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("secure/thing")
    suspend fun secure(): Response<NetworkEnvelope<Thing>>

    @GET("open/thing")
    suspend fun open(): Response<NetworkEnvelope<Thing>>
}

/** Mutable session double; null fields exercise the header-omission path. */
class FakeSessionTokenProvider(
    @Volatile var token: String? = null,
    @Volatile var tenant: String? = null,
    @Volatile var legal: String? = null,
) : SessionTokenProvider {
    override suspend fun accessToken(): String? = token

    override suspend fun tenantId(): String? = tenant

    override suspend fun legalEntityId(): String? = legal
}

/** Refresher double recording invocations; [onRefresh] can mutate the provider. */
class FakeTokenRefresher(
    private val result: Boolean = false,
    private val onRefresh: () -> Unit = {},
) : TokenRefresher {
    var count: Int = 0
        private set

    override suspend fun refresh(): Boolean {
        count++
        onRefresh()
        return result
    }
}
