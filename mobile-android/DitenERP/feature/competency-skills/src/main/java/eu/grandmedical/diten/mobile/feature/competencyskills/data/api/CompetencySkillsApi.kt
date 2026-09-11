package eu.grandmedical.diten.mobile.feature.competencyskills.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED competency-skills gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface CompetencySkillsApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/competency-skills")
    suspend fun list(): Response<NetworkEnvelope<List<CompetencySkillsReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/competency-skills/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<CompetencySkillsReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/competency-skills")
    suspend fun create(
        @Body request: CompetencySkillsCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/competency-skills/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<CompetencySkillsReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/competency-skills/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
