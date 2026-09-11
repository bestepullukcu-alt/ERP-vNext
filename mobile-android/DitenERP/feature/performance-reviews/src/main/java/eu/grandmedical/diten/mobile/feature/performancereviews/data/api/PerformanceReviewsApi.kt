package eu.grandmedical.diten.mobile.feature.performancereviews.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED performance-reviews gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface PerformanceReviewsApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/performance-reviews")
    suspend fun list(): Response<NetworkEnvelope<List<PerformanceReviewsReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/performance-reviews/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<PerformanceReviewsReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/performance-reviews")
    suspend fun create(
        @Body request: PerformanceReviewsCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/performance-reviews/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<PerformanceReviewsReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/performance-reviews/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
