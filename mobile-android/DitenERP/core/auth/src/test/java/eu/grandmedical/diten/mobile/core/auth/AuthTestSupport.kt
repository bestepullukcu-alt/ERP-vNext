package eu.grandmedical.diten.mobile.core.auth

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.network.api.AuthApi
import kotlinx.coroutines.CoroutineScope
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockWebServer
import retrofit2.Retrofit
import java.io.File
import java.util.Base64

/** Shared lenient Json matching the production NetworkModule configuration. */
internal fun testJson(): Json = Json {
    ignoreUnknownKeys = true
    explicitNulls = false
}

/** Builds an unsigned-shape JWT (`header.payload.signature`) from a raw payload JSON. */
internal fun buildJwt(payloadJson: String): String {
    val encoder = Base64.getUrlEncoder().withoutPadding()
    val header = encoder.encodeToString("""{"alg":"none","typ":"JWT"}""".encodeToByteArray())
    val payload = encoder.encodeToString(payloadJson.encodeToByteArray())
    val signature = encoder.encodeToString("signature".encodeToByteArray())
    return "$header.$payload.$signature"
}

/** Creates a file-backed preferences DataStore for a test. */
internal fun testDataStore(dir: File, scope: CoroutineScope): DataStore<Preferences> =
    PreferenceDataStoreFactory.create(scope = scope) {
        File(dir, "auth_test.preferences_pb")
    }

/** Builds a real [AuthApi] pointed at [server] so tests exercise the transport stack. */
internal fun authApiFor(server: MockWebServer, json: Json): AuthApi =
    Retrofit.Builder()
        .baseUrl(server.url("/"))
        .client(OkHttpClient())
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()
        .create(AuthApi::class.java)

internal fun safeApiCaller(json: Json): SafeApiCaller = SafeApiCaller(json)
