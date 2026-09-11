package eu.grandmedical.diten.mobile.core.auth.di

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import eu.grandmedical.diten.mobile.core.auth.crypto.KeystoreTokenCipher
import eu.grandmedical.diten.mobile.core.auth.crypto.TokenCipher
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepositoryImpl
import javax.inject.Singleton

/**
 * Binds the auth feature's own abstractions to their production implementations:
 * the domain [AuthRepository] and the [TokenCipher] used to protect tokens at
 * rest (AndroidKeyStore AES/GCM).
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class AuthModule {

    @Binds
    @Singleton
    abstract fun bindAuthRepository(impl: AuthRepositoryImpl): AuthRepository

    @Binds
    @Singleton
    abstract fun bindTokenCipher(impl: KeystoreTokenCipher): TokenCipher
}
