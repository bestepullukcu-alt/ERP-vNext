package eu.grandmedical.diten.mobile.core.common

/**
 * Minimal MVI-style result wrapper shared across feature modules.
 *
 * This is a foundation placeholder for the upcoming presentation layer: screens
 * expose their state as a [UiResult] so that loading / success / error handling
 * is uniform. Real state models arrive in later work packages.
 */
sealed interface UiResult<out T> {
    /** The operation is in progress and no value is available yet. */
    data object Loading : UiResult<Nothing>

    /** The operation completed successfully and produced [data]. */
    data class Success<out T>(val data: T) : UiResult<T>

    /** The operation failed with a human-readable [message] and an optional [cause]. */
    data class Error(val message: String, val cause: Throwable? = null) : UiResult<Nothing>
}
