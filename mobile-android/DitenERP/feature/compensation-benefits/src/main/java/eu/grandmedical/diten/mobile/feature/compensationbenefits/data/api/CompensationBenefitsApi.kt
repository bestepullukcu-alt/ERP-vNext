package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED compensation-benefits gateway endpoints.
 * Every call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the
 * request into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id
 * stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface CompensationBenefitsApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/compensation-benefits")
    suspend fun list(): Response<NetworkEnvelope<List<CompensationBenefitsReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/compensation-benefits/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<CompensationBenefitsReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/compensation-benefits")
    suspend fun create(
        @Body request: CompensationBenefitsCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/compensation-benefits/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<CompensationBenefitsReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/compensation-benefits/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
