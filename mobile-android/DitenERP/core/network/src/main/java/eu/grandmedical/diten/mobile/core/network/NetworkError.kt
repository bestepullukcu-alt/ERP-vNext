package eu.grandmedical.diten.mobile.core.network

/**
 * Categorised transport/domain failure, richer than a bare error string so that
 * callers can branch on the *kind* of failure (e.g. re-auth on [Unauthorized],
 * show field errors on [Validation]). [message] is the human-readable text that
 * feeds `UiResult.Error`.
 */
sealed class NetworkError(open val message: String) {

    /** 401 - missing/expired credentials; a refresh attempt has already failed. */
    data object Unauthorized : NetworkError("Not authorized.")

    /** 403 - authenticated but not permitted. */
    data object Forbidden : NetworkError("Access denied.")

    /** 400/422 or an envelope with `isSuccessful=false`; [messages] are the server errors. */
    data class Validation(val messages: List<String>) :
        NetworkError(messages.firstOrNull().orEmpty().ifBlank { "Request could not be processed." })

    /** 404 - resource not found. */
    data object NotFound : NetworkError("Resource not found.")

    /** Any 5xx; [code] is the concrete status. */
    data class Server(val code: Int) : NetworkError("Server error ($code).")

    /** No network / timeout / socket failure (IOException). */
    data object Connectivity : NetworkError("Cannot reach the server. Check your connection.")

    /** Anything not categorised above (malformed body, empty envelope, etc.). */
    data object Unknown : NetworkError("Something went wrong.")
}
