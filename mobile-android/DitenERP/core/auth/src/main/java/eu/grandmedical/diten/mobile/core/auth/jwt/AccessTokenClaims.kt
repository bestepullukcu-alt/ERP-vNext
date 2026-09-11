package eu.grandmedical.diten.mobile.core.auth.jwt

/**
 * Decoded, UNVERIFIED view of an access-token JWT payload, exposing only the
 * claims the mobile client acts on. The server verified the signature; the token
 * arrived over TLS, so the client trusts the payload without re-verifying it.
 *
 * Claim mapping (MEASURED backend contract):
 *  - [subject]       <- `sub`
 *  - [email]         <- `email`
 *  - [tenantId]      <- `tenant_id`
 *  - [legalEntities] <- `legal_entities` (a SINGLE comma-separated CSV string of
 *                       GUIDs, split on comma, trimmed, blanks dropped)
 *  - [permissions]   <- `permission` (string OR array)
 *  - [roles]         <- ClaimTypes.Role long URI (string OR array)
 *  - [expiresAt]     <- `exp` (epoch seconds)
 */
data class AccessTokenClaims(
    val subject: String? = null,
    val email: String? = null,
    val tenantId: String? = null,
    val legalEntities: List<String> = emptyList(),
    val permissions: List<String> = emptyList(),
    val roles: List<String> = emptyList(),
    val expiresAt: Long? = null,
)
