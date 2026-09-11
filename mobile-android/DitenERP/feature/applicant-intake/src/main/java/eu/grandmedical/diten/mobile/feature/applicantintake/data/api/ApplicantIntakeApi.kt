package eu.grandmedical.diten.mobile.feature.applicantintake.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeCreateRequestDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED applicant-intake gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface ApplicantIntakeApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/applicant-intake")
    suspend fun list(): Response<NetworkEnvelope<List<ApplicantIntakeReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/applicant-intake/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<ApplicantIntakeReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/applicant-intake")
    suspend fun create(
        @Body request: ApplicantIntakeCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/applicant-intake/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<ApplicantIntakeReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/applicant-intake/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
