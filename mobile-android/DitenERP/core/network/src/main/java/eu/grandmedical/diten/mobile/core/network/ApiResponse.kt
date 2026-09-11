package eu.grandmedical.diten.mobile.core.network

import eu.grandmedical.diten.mobile.core.common.UiResult

/**
 * Rich result of a single API call: either [Success] with the unwrapped
 * envelope payload, or [Failure] carrying a categorised [NetworkError].
 *
 * Use this when the caller needs to branch on the failure category; map to
 * [UiResult] with [toUiResult] for the presentation layer.
 */
sealed interface ApiResponse<out T> {
    data class Success<out T>(val data: T) : ApiResponse<T>

    data class Failure(val error: NetworkError) : ApiResponse<Nothing>
}

/** Collapses an [ApiResponse] into the shared [UiResult] used by screens. */
fun <T> ApiResponse<T>.toUiResult(): UiResult<T> =
    when (this) {
        is ApiResponse.Success -> UiResult.Success(data)
        is ApiResponse.Failure -> UiResult.Error(error.message)
    }
