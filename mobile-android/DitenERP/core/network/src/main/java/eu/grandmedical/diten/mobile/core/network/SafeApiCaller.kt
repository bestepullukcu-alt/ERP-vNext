package eu.grandmedical.diten.mobile.core.network

import eu.grandmedical.diten.mobile.core.common.UiResult
import kotlinx.serialization.json.Json
import retrofit2.Response
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Central adapter that turns a Retrofit `Response<NetworkEnvelope<T>>` into a
 * typed result, mapping both HTTP status codes and the backend envelope into a
 * [NetworkError] category. It never throws for *expected* HTTP errors; only
 * genuinely unexpected failures fall through to [NetworkError.Unknown], and
 * IO/timeout failures become [NetworkError.Connectivity].
 */
@Singleton
class SafeApiCaller @Inject constructor(
    private val json: Json,
) {

    /** Rich variant exposing the [NetworkError] category. */
    // Swallowing is deliberate: this adapter's contract is to translate every
    // thrown failure into a typed [NetworkError] rather than propagate it.
    @Suppress("SwallowedException")
    suspend fun <T> apiCall(block: suspend () -> Response<NetworkEnvelope<T>>): ApiResponse<T> =
        try {
            val response = block()
            if (response.isSuccessful) {
                mapSuccessfulHttp(response)
            } else {
                ApiResponse.Failure(mapHttpError(response.code(), errorsFrom(response)))
            }
        } catch (io: IOException) {
            // Covers no-connectivity, DNS, socket + read/write timeouts.
            ApiResponse.Failure(NetworkError.Connectivity)
        } catch (
            @Suppress("TooGenericExceptionCaught") unexpected: Exception,
        ) {
            ApiResponse.Failure(NetworkError.Unknown)
        }

    /** Presentation-facing variant returning the shared [UiResult]. */
    suspend fun <T> safeApiCall(block: suspend () -> Response<NetworkEnvelope<T>>): UiResult<T> =
        apiCall(block).toUiResult()

    private fun <T> mapSuccessfulHttp(response: Response<NetworkEnvelope<T>>): ApiResponse<T> {
        val envelope = response.body()
            ?: return ApiResponse.Failure(NetworkError.Unknown)
        val payload = envelope.data
        return when {
            envelope.isSuccessful && payload != null -> ApiResponse.Success(payload)
            // HTTP 200 but the domain reports failure, or a success with no body.
            envelope.errors.isNotEmpty() -> ApiResponse.Failure(NetworkError.Validation(envelope.errors))
            !envelope.isSuccessful -> ApiResponse.Failure(mapHttpError(envelope.statusCode, envelope.errors))
            else -> ApiResponse.Failure(NetworkError.Unknown)
        }
    }

    private fun mapHttpError(code: Int, errors: List<String>): NetworkError =
        when (code) {
            STATUS_BAD_REQUEST, STATUS_UNPROCESSABLE -> NetworkError.Validation(errors)
            STATUS_UNAUTHORIZED -> NetworkError.Unauthorized
            STATUS_FORBIDDEN -> NetworkError.Forbidden
            STATUS_NOT_FOUND -> NetworkError.NotFound
            in STATUS_SERVER_RANGE -> NetworkError.Server(code)
            else -> NetworkError.Unknown
        }

    /** Best-effort recovery of the error envelope from a non-2xx response body. */
    private fun errorsFrom(response: Response<*>): List<String> =
        try {
            response.errorBody()?.string()?.takeIf { it.isNotBlank() }?.let { raw ->
                json.decodeFromString(ErrorEnvelope.serializer(), raw).errors
            }.orEmpty()
        } catch (
            @Suppress("TooGenericExceptionCaught") ignored: Exception,
        ) {
            emptyList()
        }

    private companion object {
        const val STATUS_BAD_REQUEST = 400
        const val STATUS_UNAUTHORIZED = 401
        const val STATUS_FORBIDDEN = 403
        const val STATUS_NOT_FOUND = 404
        const val STATUS_UNPROCESSABLE = 422
        val STATUS_SERVER_RANGE = 500..599
    }
}
