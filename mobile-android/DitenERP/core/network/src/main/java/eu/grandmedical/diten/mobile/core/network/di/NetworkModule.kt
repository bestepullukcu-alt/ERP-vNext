package eu.grandmedical.diten.mobile.core.network.di

import eu.grandmedical.diten.mobile.core.network.BuildConfig
import eu.grandmedical.diten.mobile.core.network.CertificatePinnerProvider
import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.core.network.api.AuthApi
import eu.grandmedical.diten.mobile.core.network.interceptor.AuthAuthenticator
import eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

/**
 * Wires the OkHttp + Retrofit + kotlinx.serialization stack as singletons.
 * Base URL and logging behaviour come from [BuildConfig]; no endpoint or secret
 * is hard-coded here.
 */
@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    private const val CONNECT_TIMEOUT_SECONDS = 15L
    private const val READ_TIMEOUT_SECONDS = 30L
    private const val WRITE_TIMEOUT_SECONDS = 30L
    private const val CALL_TIMEOUT_SECONDS = 60L

    @Provides
    @Singleton
    fun provideJson(): Json =
        Json {
            ignoreUnknownKeys = true
            explicitNulls = false
            // Serialize properties even when they equal their default value. Without
            // this, request DTOs silently omit fields like sourceContractVersion or
            // the readiness state ints whenever they match the Kotlin default, so the
            // backend rejects the create ("SourceContractVersion is required.") and the
            // row is marked FAILED. Sending the full payload keeps create requests
            // valid across every feature module.
            encodeDefaults = true
        }

    @Provides
    @Singleton
    fun provideLoggingInterceptor(): HttpLoggingInterceptor =
        HttpLoggingInterceptor().apply {
            // Full bodies only when the build opts in; release is silent. Tokens
            // are redacted regardless so credentials never reach logcat.
            level = if (BuildConfig.NETWORK_LOG) {
                HttpLoggingInterceptor.Level.BODY
            } else {
                HttpLoggingInterceptor.Level.NONE
            }
            redactHeader(NetworkHeaders.AUTHORIZATION)
        }

    @Provides
    @Singleton
    fun provideOkHttpClient(
        headerInterceptor: HeaderInterceptor,
        loggingInterceptor: HttpLoggingInterceptor,
        authAuthenticator: AuthAuthenticator,
        certificatePinnerProvider: CertificatePinnerProvider,
    ): OkHttpClient =
        OkHttpClient.Builder()
            // HeaderInterceptor first so the logging interceptor (added next)
            // observes and redacts the final headers.
            .addInterceptor(headerInterceptor)
            .addInterceptor(loggingInterceptor)
            .authenticator(authAuthenticator)
            .certificatePinner(certificatePinnerProvider.pinner())
            .connectTimeout(CONNECT_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .readTimeout(READ_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .writeTimeout(WRITE_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .callTimeout(CALL_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .build()

    @Provides
    @Singleton
    fun provideRetrofit(client: OkHttpClient, json: Json): Retrofit =
        Retrofit.Builder()
            .baseUrl(BuildConfig.BASE_URL)
            .client(client)
            .addConverterFactory(json.asConverterFactory(NetworkHeaders.JSON_MEDIA_TYPE.toMediaType()))
            .build()

    @Provides
    @Singleton
    fun provideAuthApi(retrofit: Retrofit): AuthApi = retrofit.create(AuthApi::class.java)
}
