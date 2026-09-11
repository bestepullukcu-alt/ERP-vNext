package eu.grandmedical.diten.mobile.core.network

/**
 * Canonical header names used across the transport layer.
 *
 * [AUTH_MARKER] is an internal, request-scoped marker: Retrofit endpoints that
 * require authentication annotate themselves with `@Headers("X-Diten-Auth: required")`.
 * [HeaderInterceptor][eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]
 * reads it, attaches the real auth headers, and strips the marker before the
 * request leaves the process, so it never reaches the wire.
 */
object NetworkHeaders {
    const val AUTHORIZATION = "Authorization"
    const val TENANT_ID = "X-Tenant-Id"
    const val LEGAL_ENTITY_ID = "X-Legal-Entity-Id"
    const val ACCEPT = "Accept"

    const val AUTH_MARKER = "X-Diten-Auth"
    const val AUTH_MARKER_VALUE = "required"

    const val BEARER_PREFIX = "Bearer "
    const val JSON_MEDIA_TYPE = "application/json"

    /** Convenience for Retrofit `@Headers` on authenticated endpoints. */
    const val AUTHENTICATED = "$AUTH_MARKER: $AUTH_MARKER_VALUE"
}
