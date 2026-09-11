package eu.grandmedical.diten.mobile.core.network.api

import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.core.network.dto.AuthResponse
import eu.grandmedical.diten.mobile.core.network.dto.LoginRequest
import eu.grandmedical.diten.mobile.core.network.dto.MfaVerifyRequest
import eu.grandmedical.diten.mobile.core.network.dto.RefreshTokenRequest
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.Header
import retrofit2.http.Headers
import retrofit2.http.POST

/**
 * Transport contract for the gateway auth endpoints (MEASURED routes). No
 * business logic here - `:core:auth` (M0.4) consumes this interface. Note that
 * login/refresh/mfa are UNauthenticated (no [NetworkHeaders.AUTHENTICATED]
 * marker); the tenant is conveyed via the `X-Tenant-Id` header, not the body.
 */
interface AuthApi {

    @POST("api/tenant-auth/login")
    suspend fun login(
        @Header(NetworkHeaders.TENANT_ID) tenantId: String,
        @Body body: LoginRequest,
    ): Response<NetworkEnvelope<AuthResponse>>

    @POST("api/tenant-auth/mfa/verify")
    suspend fun verifyMfa(
        @Header(NetworkHeaders.TENANT_ID) tenantId: String,
        @Body body: MfaVerifyRequest,
    ): Response<NetworkEnvelope<AuthResponse>>

    @POST("api/auth/refresh-token")
    suspend fun refreshToken(
        @Body body: RefreshTokenRequest,
    ): Response<NetworkEnvelope<AuthResponse>>

    @Headers(NetworkHeaders.AUTHENTICATED)
    @POST("api/auth/logout")
    suspend fun logout(): Response<NetworkEnvelope<Unit>>
}
