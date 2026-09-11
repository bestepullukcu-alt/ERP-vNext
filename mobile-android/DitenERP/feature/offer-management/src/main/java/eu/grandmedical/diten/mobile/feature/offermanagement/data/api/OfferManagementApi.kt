package eu.grandmedical.diten.mobile.feature.offermanagement.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementCreateRequestDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED offer-management gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface OfferManagementApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/offer-management")
    suspend fun list(): Response<NetworkEnvelope<List<OfferManagementReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/offer-management/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<OfferManagementReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/offer-management")
    suspend fun create(
        @Body request: OfferManagementCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/offer-management/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<OfferManagementReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/offer-management/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
