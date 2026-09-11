package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineCreateRequestDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED candidate-pipeline gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface CandidatePipelineApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/candidate-pipeline")
    suspend fun list(): Response<NetworkEnvelope<List<CandidatePipelineReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/candidate-pipeline/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<CandidatePipelineReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/candidate-pipeline")
    suspend fun create(
        @Body request: CandidatePipelineCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/candidate-pipeline/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<CandidatePipelineReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/candidate-pipeline/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
