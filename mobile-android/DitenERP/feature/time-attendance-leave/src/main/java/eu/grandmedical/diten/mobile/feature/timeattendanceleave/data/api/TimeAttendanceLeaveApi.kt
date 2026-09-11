package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveCreateRequestDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessListItemDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Headers
import retrofit2.http.POST
import retrofit2.http.Path

/**
 * Retrofit surface for the MEASURED time-attendance-leave gateway endpoints. Every
 * call is authenticated: the `@Headers(AUTHENTICATED)` marker opts the request
 * into the network layer's Bearer + X-Tenant-Id + X-Legal-Entity-Id stamping
 * ([HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]),
 * and the marker is stripped before the request leaves the process.
 *
 * Built from the injected `Retrofit` (see the feature DI module).
 */
interface TimeAttendanceLeaveApi {

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/time-attendance-leave")
    suspend fun list(): Response<NetworkEnvelope<List<TimeAttendanceLeaveReadinessListItemDto>>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @GET("api/time-attendance-leave/{id}")
    suspend fun get(@Path("id") id: String): Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>>

    /** Returns the new record's id (Guid string) in the envelope's `data`. */
    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/time-attendance-leave")
    suspend fun create(
        @Body request: TimeAttendanceLeaveCreateRequestDto,
    ): Response<NetworkEnvelope<String>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/time-attendance-leave/{id}/evaluate")
    suspend fun evaluate(@Path("id") id: String): Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @DELETE("api/time-attendance-leave/{id}")
    suspend fun delete(@Path("id") id: String): Response<NetworkEnvelope<String>>
}
