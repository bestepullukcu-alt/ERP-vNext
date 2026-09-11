package eu.grandmedical.diten.mobile.core.network.dto

import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.Serializable

/**
 * Transport-level DTOs mirroring the MEASURED gateway auth contract. These are
 * pure serialization models (no logic, no storage); the auth *feature* that
 * uses them arrives in `:core:auth` (M0.4). They live here so the network stack
 * can prove it deserializes the real envelope end-to-end.
 */

@Serializable
data class LoginRequest(
    val email: String,
    val password: String,
    val rememberMe: Boolean = false,
)

@Serializable
data class RefreshTokenRequest(
    val accessToken: String,
    val refreshToken: String,
)

@Serializable
data class MfaVerifyRequest(
    val challengeId: String,
    val code: String,
)

/** Matches `AuthResponse` exactly; unknown/extra fields are ignored by the lenient Json. */
@Serializable
data class AuthResponse(
    val accessToken: String? = null,
    val refreshToken: String? = null,
    val expiresAt: String? = null,
    val user: JsonElement? = null,
    val requiresMfa: Boolean = false,
    val challengeId: String? = null,
    val maskedDestination: String? = null,
    val channel: String? = null,
    val mfaExpiresAt: String? = null,
    val requiresPasswordChange: Boolean = false,
)
