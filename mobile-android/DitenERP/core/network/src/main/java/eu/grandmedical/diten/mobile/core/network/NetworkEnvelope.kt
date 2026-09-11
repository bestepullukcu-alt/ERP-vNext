package eu.grandmedical.diten.mobile.core.network

import kotlinx.serialization.Serializable

/**
 * Backend response envelope wrapping every gateway endpoint.
 *
 * Mirrors the server `Response<T>` contract exactly (camelCase):
 * `{ "data": T?, "statusCode": int, "isSuccessful": bool, "errors": string[] }`.
 * On failure [data] is null and [errors] carries the human-readable messages.
 */
@Serializable
data class NetworkEnvelope<T>(
    val data: T? = null,
    val statusCode: Int = 0,
    val isSuccessful: Boolean = false,
    val errors: List<String> = emptyList(),
)

/**
 * Slim projection used to recover [errors]/[statusCode] from an HTTP *error*
 * body, where the typed [NetworkEnvelope.data] cannot be assumed to deserialize.
 */
@Serializable
data class ErrorEnvelope(
    val statusCode: Int = 0,
    val isSuccessful: Boolean = false,
    val errors: List<String> = emptyList(),
)
