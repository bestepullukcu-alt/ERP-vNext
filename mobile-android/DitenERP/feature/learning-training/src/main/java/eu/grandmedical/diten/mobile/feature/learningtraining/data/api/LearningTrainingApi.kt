package eu.grandmedical.diten.mobile.feature.learningtraining.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED learning-training gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface LearningTrainingApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/learning-training")
    suspend fun list(): Response<NetworkEnvelope<List<LearningTrainingReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/learning-training/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<LearningTrainingReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/learning-training")
    suspend fun create(
        @Body request: LearningTrainingCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/learning-training/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<LearningTrainingReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/learning-training/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
