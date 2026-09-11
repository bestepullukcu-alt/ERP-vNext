package eu.grandmedical.diten.mobile.core.auth.jwt

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.longOrNull
import java.util.Base64
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Pure-Kotlin decoder for the MIDDLE (payload) segment of a JWT. It DOES NOT
 * verify the signature: the gateway already verified it and the token reached us
 * over TLS. The job here is only to read the claims the client needs.
 *
 * Robust by design: a malformed token, bad base64, or absent claim yields empty
 * defaults rather than throwing, so a corrupt stored token can never crash the
 * session restore path.
 */
@Singleton
class JwtDecoder @Inject constructor() {

    private val json = Json { ignoreUnknownKeys = true }

    fun decode(accessToken: String?): AccessTokenClaims {
        val payload = payloadJson(accessToken) ?: return AccessTokenClaims()
        return AccessTokenClaims(
            subject = payload.stringClaim(CLAIM_SUBJECT),
            email = payload.stringClaim(CLAIM_EMAIL),
            tenantId = payload.stringClaim(CLAIM_TENANT_ID),
            legalEntities = payload.csvClaim(CLAIM_LEGAL_ENTITIES),
            permissions = payload.stringOrArrayClaim(CLAIM_PERMISSION),
            roles = payload.stringOrArrayClaim(CLAIM_ROLE_URI) +
                payload.stringOrArrayClaim(CLAIM_ROLE_SHORT),
            expiresAt = payload[CLAIM_EXP]?.jsonPrimitive?.longOrNull,
        )
    }

    // Sequential guard clauses (each an early return) read more clearly than a
    // single nested expression for this fail-fast parse path.
    @Suppress("ReturnCount")
    private fun payloadJson(accessToken: String?): JsonObject? {
        if (accessToken.isNullOrBlank()) return null
        val segments = accessToken.split('.')
        if (segments.size < MIN_JWT_SEGMENTS) return null
        return runCatching {
            val decoded = Base64.getUrlDecoder().decode(segments[1])
            json.parseToJsonElement(decoded.decodeToString()) as? JsonObject
        }.getOrNull()
    }

    private fun JsonObject.stringClaim(name: String): String? =
        (this[name] as? JsonPrimitive)?.takeIf { it.isString }?.content

    /** The `legal_entities` CSV: split on comma, trim, drop blanks. */
    private fun JsonObject.csvClaim(name: String): List<String> =
        stringClaim(name)
            ?.split(',')
            ?.map { it.trim() }
            ?.filter { it.isNotBlank() }
            .orEmpty()

    /** Handles a claim serialized as either a bare string or a JSON array. */
    private fun JsonObject.stringOrArrayClaim(name: String): List<String> =
        when (val element: JsonElement? = this[name]) {
            is JsonArray -> element.mapNotNull { (it as? JsonPrimitive)?.content?.takeIf(String::isNotBlank) }
            is JsonPrimitive -> element.content.takeIf { it.isNotBlank() }?.let { listOf(it) }.orEmpty()
            else -> emptyList()
        }

    private companion object {
        const val MIN_JWT_SEGMENTS = 2
        const val CLAIM_SUBJECT = "sub"
        const val CLAIM_EMAIL = "email"
        const val CLAIM_TENANT_ID = "tenant_id"
        const val CLAIM_LEGAL_ENTITIES = "legal_entities"
        const val CLAIM_PERMISSION = "permission"
        const val CLAIM_ROLE_URI = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        const val CLAIM_ROLE_SHORT = "role"
        const val CLAIM_EXP = "exp"
    }
}
